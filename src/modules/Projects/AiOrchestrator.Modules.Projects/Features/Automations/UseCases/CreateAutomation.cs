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
                        request.OutputLabels,
                        // Named, because PreviewPort sits between them and this endpoint has
                        // never forwarded it — a separate defect, not this change's to widen.
                        Model: request.Model,
                        ToStage: request.ToStage
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
        int? PreviewPort = null,
        /// <summary>The model this Automation's Runs think with; null inherits the deployment's (#291).</summary>
        string? Model = null,
        /// <summary>
        /// The to-stage of the one transition this Automation claims (#310). Its from-stage is the
        /// trigger label. Null or blank means it claims none: it acts, it may mark the Story, and the
        /// flow ends there (design D3).
        /// </summary>
        string? ToStage = null
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
        int? PreviewPort,
        /// <summary>The chosen model; null means the deployment's. Readable for the same reason
        /// again — an update that could not read it would clear it on every edit.</summary>
        string? Model,
        /// <summary>The to-stage this Automation claims, or null for none (#310). Readable for the
        /// same reason as every field above it: the wholesale PUT replaces the whole Automation, so a
        /// client that could not read the claim would clear it on every edit — the failure ADR-0019
        /// was written for, and the second field the board's inline request would have lost.</summary>
        string? ToStage
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
        int? PreviewPort = null,
        /// <summary>The model this Automation's Runs think with; null inherits the deployment's (#291).</summary>
        string? Model = null,
        /// <summary>The to-stage of the transition this Automation claims; null claims none (#310).</summary>
        string? ToStage = null
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

            // The claimed transition's to-stage is a stage name, bounded exactly as the trigger
            // label it will be compared against. Absent means "claims no transition" (design D3).
            RuleFor(command => command.ToStage).MaximumLength(200);

            // An Automation whose applied label is its own trigger re-fires itself: the first Run
            // succeeds, writes the label, and matching declines the second because BR-001 sees an
            // active Run — leaving a labelled Story, no work, and nothing saying why. Refused
            // here because it is a relation between two fields of this request, not a conflict
            // with stored state (#115 design D3).
            // Every member, not one (#165): the rule is about the relation between what is applied
            // and the trigger, so a set of three with the trigger third is the same defect as a set
            // of one.
            //
            // #310 extends this one refusal rather than adding a second beside it: after the
            // transition/mark split there are two fields a Run applies from, and a to-stage equal to
            // the trigger is the same loop spelled in the new field. Both travel Applied().
            //
            // Case-insensitive, unlike the version this replaces. The vendor treats AI:Implement and
            // ai:implement as one label (DEC-056) and BR-003's identity already does, so an ordinal
            // comparison let a differently-cased self-trigger through — the exact loop the rule
            // exists to prevent, spelled differently.
            RuleFor(command => command.TriggerLabel)
                .Must(
                    (command, _) =>
                        !Applied(command.ToStage, command.OutputLabels)
                            .Any(label => Automation.SameLabel(label, command.TriggerLabel))
                )
                .WithMessage(
                    "An Automation cannot hand work to itself: one of the labels it applies — its "
                        + "to-stage or one of its marks — is its own trigger label."
                );

            // A mark repeating the to-stage would apply the same label twice through the same write
            // and draw a boundary the board already draws. The set's own dedupe cannot see this one,
            // because the to-stage is no longer in the set (#310, design D9).
            RuleFor(command => command.OutputLabels!)
                .Must(
                    (command, labels) =>
                        !labels.Any(label => Automation.SameLabel(label, command.ToStage))
                )
                .When(command =>
                    command.OutputLabels is not null && !string.IsNullOrWhiteSpace(command.ToStage)
                )
                .WithMessage(
                    "A mark cannot repeat the to-stage: the claimed transition already applies it."
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
            // Loaded rather than merely counted since #310: a claim creates the stages it names, so
            // the Project is part of this write and not only a precondition for it.
            var project = await database.Projects.FirstOrDefaultAsync(
                entity => entity.Id == command.ProjectId,
                cancellationToken
            );

            if (project is null)
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
                command.PreviewPort,
                command.Model,
                command.ToStage
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

            // The claim and the stages it creates are one write (#310, design D4/D9). BR-003 is
            // asked first, so a refused Automation leaves the lifecycle untouched even in memory —
            // and the adjacency guard refuses before mutating, so the same holds for it.
            var claim = project.ClaimTransition(candidate.TriggerLabel, candidate.ToStage);
            if (claim.IsError)
            {
                return claim.Errors;
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

    /// <summary>
    /// Every label a successful Run applies: the claimed transition's to-stage where there is one,
    /// and every mark. One sequence and one caller-visible rule, because the self-trigger refusal is
    /// about the relation between <i>what an Automation applies</i> and <i>what makes it fire</i> —
    /// and after #310's split that is two fields rather than one. Both validators read it, so the
    /// refusal cannot come to mean two different things on the two paths.
    /// </summary>
    internal static IEnumerable<string> Applied(string? toStage, IReadOnlyList<string>? marks) =>
        (string.IsNullOrWhiteSpace(toStage) ? Enumerable.Empty<string>() : [toStage]).Concat(
            marks ?? []
        );

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
            automation.PreviewPort,
            automation.Model,
            automation.ToStage
        );
}
