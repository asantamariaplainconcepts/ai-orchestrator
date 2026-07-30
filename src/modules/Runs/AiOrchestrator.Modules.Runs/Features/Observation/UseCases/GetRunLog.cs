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
/// UC-027 — the Run's log so far, and whether it is done. The client polls while
/// <see cref="Response.Complete"/> is false and stops itself (design D3): no push, no
/// negotiation, and a finished Run read later is the same endpoint answering complete on the
/// first response.
/// </summary>
sealed class GetRunLog : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/runs/{runId:guid}/log",
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
            .WithName(nameof(GetRunLog))
            .WithTags("Runs");

    [Requires(RunPermissions.Read)]
    internal sealed record Query(Guid ProjectId, Guid RunId) : IQuery<Response?>, IScopedToProject;

    /// <summary>
    /// <paramref name="NextSequence"/> is where the next chunk will be (#144, design D5): a client
    /// that subscribed before this read uses it to drop the overlap instead of appending it twice.
    /// </summary>
    internal sealed record Response(string Content, bool Complete, int NextSequence);

    internal sealed class Handler(RunsDbContext database) : IAppQueryHandler<Query, Response?>
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

            var chunks = await database
                .LogChunks.Where(chunk => chunk.RunId == query.RunId)
                .OrderBy(chunk => chunk.Sequence)
                .Select(chunk => new { chunk.Sequence, chunk.Content })
                .ToListAsync(cancellationToken);

            // Waiting states are "not executing right now" but not done: the page keeps the
            // log visible and stops the fast poll on terminal only — a resumed pass appends to
            // the same log, and a watcher should see it continue.
            // Terminal derived from BR-001's own list, not a third hand-written copy of it: the
            // comment on RunStates records that such copies have drifted twice already, and this
            // file held one that happened to still agree.
            return new Response(
                string.Join('\n', chunks.Select(chunk => chunk.Content)),
                RunStates.IsTerminal(run.State),
                chunks.Count == 0 ? 0 : chunks[^1].Sequence + 1
            );
        }
    }
}
