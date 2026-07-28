using AiOrchestrator.BuildingBlocks.CQS;
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

    internal sealed record Query(Guid ProjectId, Guid RunId) : IQuery<Response?>;

    internal sealed record Response(string Content, bool Complete);

    internal sealed class Handler(RunsDbContext database) : IAppQueryHandler<Query, Response?>
    {
        static readonly RunState[] Terminal =
        [
            RunState.Succeeded,
            RunState.Failed,
            RunState.Cancelled,
        ];

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

            var lines = await database
                .LogChunks.Where(chunk => chunk.RunId == query.RunId)
                .OrderBy(chunk => chunk.Sequence)
                .Select(chunk => chunk.Content)
                .ToListAsync(cancellationToken);

            // Waiting states are "not executing right now" but not done: the page keeps the
            // log visible and stops the fast poll on terminal only — a resumed pass appends to
            // the same log, and a watcher should see it continue.
            return new Response(string.Join('\n', lines), Terminal.Contains(run.State));
        }
    }
}
