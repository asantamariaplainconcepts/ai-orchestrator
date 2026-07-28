using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog.UseCases;

/// <summary>
/// The ambient health read (#97): one row per configured Connector, for the projects list to
/// join client-side — the same pattern the runs list uses for automation details. Renders what
/// the poller already records; no new probing exists anywhere in this slice (BR-008: the mirror
/// serves stale data on failure by design, which is exactly why the staleness signal earns an
/// ambient surface).
/// </summary>
sealed class ListConnectors : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/connectors",
                async (ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(ListConnectors))
            .WithTags("Backlog");

    internal sealed record Query : IQuery<IReadOnlyList<Response>>;

    /// <summary>Note what is absent: no secret name even — the list needs health, not config.</summary>
    internal sealed record Response(
        Guid ProjectId,
        string Vendor,
        DateTimeOffset? LastSyncedAt,
        string? LastFailure,
        DateTimeOffset? LastFailureAt
    );

    internal sealed class Handler(BacklogDbContext database)
        : IAppQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<IReadOnlyList<Response>> Handle(
            Query query,
            CancellationToken cancellationToken
        )
        {
            // Materialise then project (the #7 lesson): ToString() inside an EF projection
            // translates to SQL and names the vendor by its ordinal, which is exactly what the
            // existing read tests forbid.
            var connectors = await database.Connectors.ToListAsync(cancellationToken);

            return
            [
                .. connectors.Select(entity => new Response(
                    entity.ProjectId,
                    entity.Vendor.ToString(),
                    entity.LastSyncedAt,
                    entity.LastFailure,
                    entity.LastFailureAt
                )),
            ];
        }
    }
}
