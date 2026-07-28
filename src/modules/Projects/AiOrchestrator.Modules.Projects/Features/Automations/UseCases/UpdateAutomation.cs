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
/// UC-006 — correcting an Automation. An edit is another way to create a BR-003 overlap, so it
/// faces the same gate a create does; the only difference is that the Automation is excluded
/// from its own comparison (design D1/D2).
/// </summary>
sealed class UpdateAutomation : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPut(
                "/api/projects/{projectId:guid}/automations/{automationId:guid}",
                async (
                    Guid projectId,
                    Guid automationId,
                    CreateAutomation.Request request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Command(
                            projectId,
                            automationId,
                            request.TriggerLabel,
                            request.TriggerState,
                            request.Action,
                            request.Runtime,
                            request.RequiresApproval,
                            request.TimeoutMinutes,
                            request.RubricPath,
                            request.OutputLabel
                        ),
                        cancellationToken
                    );
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .WithName(nameof(UpdateAutomation))
            .WithTags("Automations");

        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/automations/{automationId:guid}/enable",
                async (
                    Guid projectId,
                    Guid automationId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new SetEnabled(projectId, automationId, Enabled: true),
                        cancellationToken
                    );
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .WithName("EnableAutomation")
            .WithTags("Automations");

        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/automations/{automationId:guid}/disable",
                async (
                    Guid projectId,
                    Guid automationId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new SetEnabled(projectId, automationId, Enabled: false),
                        cancellationToken
                    );
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .WithName("DisableAutomation")
            .WithTags("Automations");
    }

    internal sealed record Command(
        Guid ProjectId,
        Guid AutomationId,
        string TriggerLabel,
        string? TriggerState,
        string Action,
        string Runtime,
        bool RequiresApproval,
        int? TimeoutMinutes,
        string? RubricPath = null,
        string? OutputLabel = null
    ) : ICommand<ErrorOr<CreateAutomation.Response>>;

    internal sealed record SetEnabled(Guid ProjectId, Guid AutomationId, bool Enabled)
        : ICommand<ErrorOr<CreateAutomation.Response>>;

    /// <summary>The same input rules as a create — an edit cannot be laxer than the thing it edits.</summary>
    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.TriggerLabel).NotEmpty().MaximumLength(200);
            RuleFor(command => command.TriggerState).MaximumLength(100);
            // The length bounds were on create but not here, so an edit could store a value the
            // column refuses. Same bounds, same place, both directions.
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
                .InclusiveBetween(1, 720)
                .When(command => command.TimeoutMinutes.HasValue);
        }
    }

    internal sealed class Handler(ProjectsDbContext database, OverlapGuard overlaps)
        : IAppCommandHandler<Command, ErrorOr<CreateAutomation.Response>>
    {
        public async Task<ErrorOr<CreateAutomation.Response>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var automation = await database.Automations.FirstOrDefaultAsync(
                entity =>
                    entity.Id == command.AutomationId && entity.ProjectId == command.ProjectId,
                cancellationToken
            );

            if (automation is null)
            {
                return ProjectErrors.AutomationNotFound(command.AutomationId);
            }

            automation.UpdateTo(
                command.TriggerLabel,
                string.IsNullOrWhiteSpace(command.TriggerState) ? null : command.TriggerState,
                Enum.Parse<AutomationAction>(command.Action),
                Enum.Parse<AgentRuntime>(command.Runtime),
                command.RequiresApproval,
                command.TimeoutMinutes is { } minutes
                    ? TimeSpan.FromMinutes(minutes)
                    : CreateAutomation.DefaultTimeout,
                string.IsNullOrWhiteSpace(command.RubricPath) ? null : command.RubricPath,
                string.IsNullOrWhiteSpace(command.OutputLabel) ? null : command.OutputLabel
            );

            // Excluding itself: an Automation must not be refused for colliding with the
            // version of itself it is replacing.
            var overlap = await overlaps.Check(
                automation,
                command.ProjectId,
                excluding: automation.Id,
                cancellationToken
            );
            if (overlap.IsError)
            {
                // Nothing is saved, and the tracked entity is discarded with the scope — the
                // stored Automation is exactly as it was.
                return overlap.Errors;
            }

            await database.SaveChangesAsync(cancellationToken);
            return CreateAutomation.ToResponse(automation);
        }
    }

    internal sealed class EnabledHandler(ProjectsDbContext database, OverlapGuard overlaps)
        : IAppCommandHandler<SetEnabled, ErrorOr<CreateAutomation.Response>>
    {
        public async Task<ErrorOr<CreateAutomation.Response>> Handle(
            SetEnabled command,
            CancellationToken cancellationToken
        )
        {
            var automation = await database.Automations.FirstOrDefaultAsync(
                entity =>
                    entity.Id == command.AutomationId && entity.ProjectId == command.ProjectId,
                cancellationToken
            );

            if (automation is null)
            {
                return ProjectErrors.AutomationNotFound(command.AutomationId);
            }

            automation.SetEnabled(command.Enabled);

            // Enabling can introduce an overlap because the world moved while it was off;
            // disabling never can (design D2).
            if (command.Enabled)
            {
                var overlap = await overlaps.Check(
                    automation,
                    command.ProjectId,
                    excluding: automation.Id,
                    cancellationToken
                );
                if (overlap.IsError)
                {
                    return overlap.Errors;
                }
            }

            await database.SaveChangesAsync(cancellationToken);
            return CreateAutomation.ToResponse(automation);
        }
    }
}
