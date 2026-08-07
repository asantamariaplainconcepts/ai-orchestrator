using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Projects.Domain;
using AiOrchestrator.Modules.Projects.Persistence;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Projects.Features.Automations.UseCases;

/// <summary>
/// UC-005 — an Admin says what a trigger label makes an Agent do.
/// <para>
/// The interesting part is the refusal: BR-003 forbids two enabled Automations whose triggers
/// could match one Story, and DEC-033 puts that gate at save time so the runtime never has to
/// choose between two matches.
/// </para>
/// </summary>
sealed class CreateAutomation : IUseCase
{
    /// <summary>BR-005's default and ceiling, which live in the shared kernel because the
    /// contract spans modules — the dispatch worker enforces the same ceiling (#144, DEC-054).</summary>
    public static readonly TimeSpan DefaultTimeout = PhaseBudget.Default;

    public const int MaximumTimeoutMinutes = PhaseBudget.MaximumMinutes;

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/automations",
                async (
                    Guid projectId,
                    Request request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new Command(
                        projectId,
                        request.TriggerLabel,
                        request.TriggerState,
                        request.Action,
                        request.Runtime,
                        request.RequiresApproval,
                        request.TimeoutMinutes,
                        request.PromptPath,
                        request.OutputLabels
                    );

                    var result = await sender.Send(command, cancellationToken);

                    return result.Match(
                        response =>
                            Results.Created(
                                $"/api/projects/{projectId}/automations/{response.Id}",
                                response
                            ),
                        ApiResults.Problem
                    );
                }
            )
            .WithName(nameof(CreateAutomation))
            .WithTags("Automations");

    internal sealed record Request(
        string TriggerLabel,
        string? TriggerState,
        string Action,
        string? Runtime,
        bool RequiresApproval,
        int? TimeoutMinutes,
        string? PromptPath = null,
        IReadOnlyList<string>? OutputLabels = null,
        int? PreviewPort = null
    );

    internal sealed record Response(
        Guid Id,
        string TriggerLabel,
        string? TriggerState,
        string Action,
        string? Runtime,
        bool RequiresApproval,
        int TimeoutMinutes,
        bool Enabled,
        /// <summary>What this Automation hands on when it succeeds (#115/#165). The canvas derives
        /// one edge per member that equals another Automation's trigger label (#116), so it has to
        /// be readable, not merely writable.</summary>
        IReadOnlyList<string> OutputLabels,
        /// <summary>Grill only. Readable for the same reason: the update endpoint replaces the
        /// whole Automation, so a caller that cannot read this field would silently clear it on
        /// every edit.</summary>
        string? PromptPath,
        /// <summary>The sandbox port published while a Run executes; null means no preview.
        /// Readable for the same reason PromptPath is.</summary>
        int? PreviewPort
    );

    [Requires(ProjectPermissions.ManageAutomations)]
    internal sealed record Command(
        Guid ProjectId,
        string TriggerLabel,
        string? TriggerState,
        string Action,
        string? Runtime,
        bool RequiresApproval,
        int? TimeoutMinutes,
        string? PromptPath = null,
        IReadOnlyList<string>? OutputLabels = null,
        int? PreviewPort = null
    ) : ICommand<ErrorOr<Response>>, IScopedToProject;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.TriggerLabel).NotEmpty().MaximumLength(200);
            RuleFor(command => command.TriggerState).MaximumLength(100);
            // Required since #162. With one action, an Automation that names no prompt can never
            // run — and the spec already forbids a configurable thing that silently never executes.
            // Refused at save, where the Admin is looking, rather than at the first Run in front of
            // somebody who did not configure it.
            RuleFor(command => command.PromptPath)
                .NotEmpty()
                .WithMessage("An Automation must name the prompt it runs.")
                .MaximumLength(300);
            // Each member bounded exactly as the single label was, and the collection bounded too:
            // a set is a field a caller controls the size of.
            // A port is a port. Refused at save rather than by docker at Run time, where the
            // person reading the failure did not choose the number.
            RuleFor(command => command.PreviewPort!.Value)
                .InclusiveBetween(1, 65535)
                .WithMessage("A preview port must be between 1 and 65535.")
                .When(command => command.PreviewPort is not null);
            RuleForEach(command => command.OutputLabels!)
                .NotEmpty()
                .MaximumLength(200)
                .When(command => command.OutputLabels is not null);
            RuleFor(command => command.OutputLabels!)
                .Must(labels => labels.Count <= 10)
                .When(command => command.OutputLabels is not null)
                .WithMessage("An Automation can hand on at most 10 labels.");

            // An Automation whose output label is its own trigger re-fires itself: the first Run
            // succeeds, writes the label, and matching declines the second because BR-001 sees an
            // active Run — leaving a labelled Story, no work, and nothing saying why. Refused
            // here because it is a relation between two fields of this request, not a conflict
            // with stored state (#115 design D3).
            // Every member, not one (#165): the rule is about the relation between the set and the
            // trigger, so a set of three with the trigger third is the same defect as a set of one.
            //
            // Case-insensitive, unlike the version this replaces. The vendor treats AI:Implement and
            // ai:implement as one label (DEC-056) and BR-003's identity already does, so an ordinal
            // comparison let a differently-cased self-trigger through — the exact loop the rule
            // exists to prevent, spelled differently.
            RuleFor(command => command.OutputLabels!)
                .Must(
                    (command, labels) =>
                        !labels.Any(label =>
                            string.Equals(
                                label,
                                command.TriggerLabel,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                )
                .When(command => command.OutputLabels is not null)
                .WithMessage(
                    "An Automation cannot hand work to itself: one of its output labels is its own trigger label."
                );

            // Parseable-to-the-enum is input validation, not a domain rule: an unknown action is
            // a malformed request, never a business conflict.
            RuleFor(command => command.Action)
                .Must(value => Enum.TryParse<AutomationAction>(value, out _))
                .WithMessage(
                    $"Action must be one of: {string.Join(", ", Enum.GetNames<AutomationAction>())}."
                );

            // Optional since project-runtimes: absent means the Project default, resolved at
            // execution time. A value that is present must still be a real runtime.
            RuleFor(command => command.Runtime)
                .Must(value => value is null || Enum.TryParse<AgentRuntime>(value, out _))
                .WithMessage(
                    $"Runtime must be one of: {string.Join(", ", Enum.GetNames<AgentRuntime>())}."
                );

            RuleFor(command => command.TimeoutMinutes)
                .InclusiveBetween(1, MaximumTimeoutMinutes)
                .WithMessage(
                    $"A phase timeout must be between 1 and {MaximumTimeoutMinutes} minutes. The "
                        + "ceiling exists so the platform budget that hosts a phase is provably "
                        + "sufficient (DEC-054)."
                )
                .When(command => command.TimeoutMinutes.HasValue);
        }
    }

    internal sealed class Handler(ProjectsDbContext database, OverlapGuard overlaps)
        : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var projectExists = await database.Projects.AnyAsync(
                project => project.Id == command.ProjectId,
                cancellationToken
            );

            if (!projectExists)
            {
                return ProjectErrors.NotFound(command.ProjectId);
            }

            var candidate = Automation.Create(
                command.ProjectId,
                command.TriggerLabel,
                string.IsNullOrWhiteSpace(command.TriggerState) ? null : command.TriggerState,
                Enum.Parse<AutomationAction>(command.Action),
                command.Runtime is null ? null : Enum.Parse<AgentRuntime>(command.Runtime),
                command.RequiresApproval,
                command.TimeoutMinutes is { } minutes
                    ? TimeSpan.FromMinutes(minutes)
                    : DefaultTimeout,
                string.IsNullOrWhiteSpace(command.PromptPath) ? null : command.PromptPath,
                command.OutputLabels is null ? [] : Clean(command.OutputLabels),
                command.PreviewPort
            );

            var overlap = await overlaps.Check(
                candidate,
                command.ProjectId,
                excluding: null,
                cancellationToken
            );
            if (overlap.IsError)
            {
                return overlap.Errors;
            }

            database.Automations.Add(candidate);

            try
            {
                await database.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (OverlapGuard.IsDuplicateTrigger(exception))
            {
                // The guard said yes and the index said no, which means a concurrent save won
                // (#147, design D2). Same refusal, discovered a moment later.
                return OverlapGuard.RaceLost(candidate);
            }

            return ToResponse(candidate);
        }
    }

    /// <summary>
    /// What actually gets stored: blanks dropped and edges trimmed, so a form that submitted an empty
    /// row does not persist one. Deduplication is the aggregate's, not this slice's — it is a rule
    /// about what an Automation *is*, and both endpoints would otherwise have to remember it.
    /// </summary>
    internal static IReadOnlyList<string> Clean(IReadOnlyList<string> labels) =>
        [.. labels.Where(label => !string.IsNullOrWhiteSpace(label)).Select(label => label.Trim())];

    internal static Response ToResponse(Automation automation) =>
        new(
            automation.Id,
            automation.TriggerLabel,
            automation.TriggerState,
            automation.Action.ToString(),
            automation.Runtime?.ToString(),
            automation.RequiresApproval,
            (int)automation.Timeout.TotalMinutes,
            automation.Enabled,
            automation.OutputLabels,
            automation.PromptPath,
            automation.PreviewPort
        );
}
