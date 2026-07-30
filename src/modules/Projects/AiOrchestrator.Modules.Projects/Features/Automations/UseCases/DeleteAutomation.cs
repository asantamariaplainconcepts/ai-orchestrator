using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Projects.Domain;
using AiOrchestrator.Modules.Projects.Persistence;
using AiOrchestrator.Modules.Runs.Contracts;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Projects.Features.Automations.UseCases;

/// <summary>
/// UC-006's missing half — removal. The rule is one sentence: <b>delete what was never used,
/// disable what was.</b>
/// <para>
/// The refusal is not caution; it is two rules already in this codebase. BR-014 lists the
/// Automation among what every Run records and says Runs are never deleted, so removing one
/// decays the audit trail backwards. And #14 found that the executor resolves the Automation
/// <i>mid-Run</i> — which is why <c>Detail</c> deliberately stopped filtering on
/// <c>Enabled</c>. A row that vanishes underneath a running Run kills it with a message that
/// is not even true ("no longer enabled"), and unlike disabling, nobody can undo it.
/// </para>
/// </summary>
sealed class DeleteAutomation : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapDelete(
                "/api/projects/{projectId:guid}/automations/{automationId:guid}",
                async (
                    Guid projectId,
                    Guid automationId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Command(projectId, automationId),
                        cancellationToken
                    );

                    return result.Match(_ => Results.NoContent(), ApiResults.Problem);
                }
            )
            .WithName(nameof(DeleteAutomation))
            .WithTags("Automations");

    [Requires(ProjectPermissions.ManageAutomations)]
    internal sealed record Command(Guid ProjectId, Guid AutomationId)
        : ICommand<ErrorOr<Deleted>>,
            IScopedToProject;

    internal sealed class Handler(ProjectsDbContext database, IRunUsage runs)
        : IAppCommandHandler<Command, ErrorOr<Deleted>>
    {
        public async Task<ErrorOr<Deleted>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            // Resolved on both ids (design D4). An Automation from another project is "not
            // found", never "forbidden": an error must not tell a caller what exists elsewhere.
            var automation = await database.Automations.FirstOrDefaultAsync(
                entity =>
                    entity.Id == command.AutomationId && entity.ProjectId == command.ProjectId,
                cancellationToken
            );

            if (automation is null)
            {
                return ProjectErrors.AutomationNotFound(command.AutomationId);
            }

            var used = await runs.CountForAutomation(
                command.ProjectId,
                command.AutomationId,
                cancellationToken
            );

            if (used > 0)
            {
                return ProjectErrors.AutomationInUse(automation.TriggerLabel, used);
            }

            database.Automations.Remove(automation);
            await database.SaveChangesAsync(cancellationToken);

            return Result.Deleted;
        }
    }
}
