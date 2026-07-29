using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
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
                        request.RubricPath,
                        request.OutputLabel
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
        string Runtime,
        bool RequiresApproval,
        int? TimeoutMinutes,
        string? RubricPath = null,
        string? OutputLabel = null
    );

    internal sealed record Response(
        Guid Id,
        string TriggerLabel,
        string? TriggerState,
        string Action,
        string Runtime,
        bool RequiresApproval,
        int TimeoutMinutes,
        bool Enabled,
        /// <summary>What this Automation hands on when it succeeds (#115). The canvas derives
        /// an edge wherever this equals another Automation's trigger label (#116), so it has to
        /// be readable, not merely writable.</summary>
        string? OutputLabel,
        /// <summary>Grill only. Readable for the same reason: the update endpoint replaces the
        /// whole Automation, so a caller that cannot read this field would silently clear it on
        /// every edit.</summary>
        string? RubricPath
    );

    internal sealed record Command(
        Guid ProjectId,
        string TriggerLabel,
        string? TriggerState,
        string Action,
        string Runtime,
        bool RequiresApproval,
        int? TimeoutMinutes,
        string? RubricPath = null,
        string? OutputLabel = null
    ) : ICommand<ErrorOr<Response>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.TriggerLabel).NotEmpty().MaximumLength(200);
            RuleFor(command => command.TriggerState).MaximumLength(100);
            RuleFor(command => command.RubricPath).MaximumLength(300);
            RuleFor(command => command.OutputLabel).MaximumLength(200);

            // An Automation whose output label is its own trigger re-fires itself: the first Run
            // succeeds, writes the label, and matching declines the second because BR-001 sees an
            // active Run — leaving a labelled Story, no work, and nothing saying why. Refused
            // here because it is a relation between two fields of this request, not a conflict
            // with stored state (#115 design D3).
            RuleFor(command => command.OutputLabel)
                .Must(
                    (command, output) =>
                        !string.Equals(output, command.TriggerLabel, StringComparison.Ordinal)
                )
                .When(command => !string.IsNullOrWhiteSpace(command.OutputLabel))
                .WithMessage(
                    "An Automation cannot hand work to itself: its output label is its own trigger label."
                );

            // Parseable-to-the-enum is input validation, not a domain rule: an unknown action is
            // a malformed request, never a business conflict.
            RuleFor(command => command.Action)
                .Must(value => Enum.TryParse<AutomationAction>(value, out _))
                .WithMessage(
                    $"Action must be one of: {string.Join(", ", Enum.GetNames<AutomationAction>())}."
                );

            RuleFor(command => command.Runtime)
                .Must(value => Enum.TryParse<AgentRuntime>(value, out _))
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
                Enum.Parse<AgentRuntime>(command.Runtime),
                command.RequiresApproval,
                command.TimeoutMinutes is { } minutes
                    ? TimeSpan.FromMinutes(minutes)
                    : DefaultTimeout,
                string.IsNullOrWhiteSpace(command.RubricPath) ? null : command.RubricPath,
                string.IsNullOrWhiteSpace(command.OutputLabel) ? null : command.OutputLabel
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
            await database.SaveChangesAsync(cancellationToken);

            return ToResponse(candidate);
        }
    }

    internal static Response ToResponse(Automation automation) =>
        new(
            automation.Id,
            automation.TriggerLabel,
            automation.TriggerState,
            automation.Action.ToString(),
            automation.Runtime.ToString(),
            automation.RequiresApproval,
            (int)automation.Timeout.TotalMinutes,
            automation.Enabled,
            automation.OutputLabel,
            automation.RubricPath
        );
}
