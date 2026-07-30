using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Projects.Domain;
using AiOrchestrator.Modules.Projects.Persistence;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Projects.Features.Automations.UseCases;

/// <summary>
/// UC-005 at scale — an Admin configures a project the way this framework intends, in one act.
/// <para>
/// Nothing here tracks whether defaults were "already applied". BR-003 refuses an overlapping
/// trigger, so a second application creates nothing on its own; a seeded-flag would be a second
/// source of truth that goes stale the first time somebody edits an Automation by hand
/// (design D2).
/// </para>
/// <para>
/// The consequence is that <b>partial success is the normal outcome</b>, and the response says
/// so. Refusing the whole operation because one trigger was taken would make the action unusable
/// on precisely the projects that most need the rest of it.
/// </para>
/// </summary>
sealed class ApplyAutomationDefaults : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/automations/defaults",
                async (Guid projectId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Command(projectId), cancellationToken);

                    return result.Match(response => Results.Ok(response), ApiResults.Problem);
                }
            )
            .WithName(nameof(ApplyAutomationDefaults))
            .WithTags("Automations");

    /// <summary>
    /// <paramref name="LabelNote"/> is null when every trigger label is present at the vendor,
    /// and otherwise says what stopped that — including the ordinary cases of a project with no
    /// Connector, and a vendor with no repository-level labels at all.
    /// </summary>
    internal sealed record Response(
        IReadOnlyList<CreateAutomation.Response> Created,
        IReadOnlyList<SkippedDefault> Skipped,
        string? LabelNote
    );

    internal sealed record SkippedDefault(string TriggerLabel, string Reason);

    [Requires(Access.AdminOfProject)]
    internal sealed record Command(Guid ProjectId) : ICommand<ErrorOr<Response>>, IScopedToProject;

    internal sealed class Handler(
        ProjectsDbContext database,
        OverlapGuard overlaps,
        ILabelWriter labels
    ) : IAppCommandHandler<Command, ErrorOr<Response>>
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

            var created = new List<Automation>();
            var skipped = new List<SkippedDefault>();

            foreach (var entry in AutomationDefaults.All)
            {
                var candidate = entry.ToAutomation(command.ProjectId);

                var overlap = await overlaps.Check(
                    candidate,
                    command.ProjectId,
                    excluding: null,
                    cancellationToken
                );

                if (overlap.IsError)
                {
                    // The rule that makes this idempotent, read as information rather than as a
                    // failure: something already handles this trigger, which is the state the
                    // Admin wanted anyway.
                    skipped.Add(
                        new SkippedDefault(entry.TriggerLabel, overlap.FirstError.Description)
                    );
                    continue;
                }

                database.Automations.Add(candidate);
                created.Add(candidate);

                // Saved one at a time so the guard sees the previous ones. Two defaults cannot
                // overlap each other today, but a future default that did would otherwise slip
                // past a check that had only queried the database.
                await database.SaveChangesAsync(cancellationToken);
            }

            // After the Automations, never before (design D4). A vendor outage must not be able
            // to leave the project with nothing, having skipped the part that needs no vendor.
            var labelNote = await labels.EnsureLabels(
                command.ProjectId,
                AutomationDefaults.Labels,
                cancellationToken
            );

            return new Response(
                [.. created.Select(CreateAutomation.ToResponse)],
                skipped,
                labelNote
            );
        }
    }
}
