using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Features.Observation.UseCases;

/// <summary>
/// Whether this Run can be opened in a terminal <b>right now</b> (#304). <see cref="GetRunPreview"/>'s
/// sibling, answering the same way and for the same reason: from the Run's own state and the machine's
/// own record, never from a stored field — which is what makes "a finished Run offers nothing" a
/// property rather than a branch somebody has to remember.
/// <para>
/// It is a <b>read</b>, so it asks for <see cref="RunPermissions.Read"/> and reports whether the
/// caller may attach as a fact. Seeing that a terminal exists and being allowed to type in it are
/// different questions, and rendering the affordance is not the same as granting it — the hub asks for
/// <see cref="RunPermissions.Attach"/> itself, and is the only thing that may.
/// </para>
/// </summary>
sealed class GetRunTerminal : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/runs/{runId:guid}/terminal",
                async (
                    Guid projectId,
                    Guid runId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var response = await sender.Send(
                        new Query(projectId, runId),
                        cancellationToken
                    );
                    return response is null ? Results.NotFound() : Results.Ok(response);
                }
            )
            .WithName(nameof(GetRunTerminal))
            .WithTags("Runs");

    [Requires(RunPermissions.Read)]
    internal sealed record Query(Guid ProjectId, Guid RunId) : IQuery<Response?>, IScopedToProject;

    /// <summary>
    /// Three answers, kept apart because each has its own remedy and its own sentence (design D5).
    /// <paramref name="Hosted"/> is the habitat's — ADR-0021 permits a terminal in self-host and
    /// refuses it in a deployment. <paramref name="Available"/> is this Run's. <paramref name="Permitted"/>
    /// is this caller's, and it is reported rather than enforced here: a reader who may not attach is
    /// shown why instead of being shown nothing.
    /// </summary>
    internal sealed record Response(bool Hosted, bool Available, bool Permitted);

    internal sealed class Handler(
        RunsDbContext database,
        IRunSandboxMonitor sandboxes,
        IRunTerminalHost terminals,
        IProjectPermissions permissions,
        Microsoft.Extensions.Options.IOptions<PermissionGrants> grants
    ) : IAppQueryHandler<Query, Response?>
    {
        public async Task<Response?> Handle(Query query, CancellationToken cancellationToken)
        {
            var run = await database
                .Runs.Where(entity =>
                    entity.Id == query.RunId && entity.ProjectId == query.ProjectId
                )
                .Select(entity => new { entity.State })
                .FirstOrDefaultAsync(cancellationToken);

            if (run is null)
            {
                return null;
            }

            // All three conditions, and none of them alone: a ledger entry for a Run that has gone
            // terminal names a sandbox being disposed, and a live Run with no entry has none to enter.
            var available =
                terminals.Hosted
                && !RunStates.IsTerminal(run.State)
                && sandboxes.NameFor(query.RunId) is not null;

            var role = await permissions.RoleOn(query.ProjectId, cancellationToken);
            var permitted =
                role is not null && grants.Value.Holds(role.Value, RunPermissions.Attach);

            return new Response(terminals.Hosted, available, permitted);
        }
    }
}
