using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog.UseCases;

/// <summary>
/// UC-007 — the project's backlog as the application knows it.
/// <para>
/// Returns the Connector's health alongside the Stories, because "no Stories" and "the last poll
/// failed" are different facts and a client that cannot tell them apart will show the wrong one.
/// </para>
/// </summary>
sealed class GetBacklog : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/backlog",
                async (Guid projectId, ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new Query(projectId), cancellationToken))
            )
            .WithName(nameof(GetBacklog))
            .WithTags("Backlog");

    internal sealed record ConnectorView(
        string Vendor,
        string Owner,
        string Repository,
        string SecretName,
        /// <summary>When the product itself stored the token (#124); null when it did not.</summary>
        DateTimeOffset? SecretSetAt,
        string? CodeRepository,
        DateTimeOffset? LastSyncedAt,
        string? LastFailure,
        DateTimeOffset? LastFailureAt
    );

    internal sealed record StoryView(
        string VendorId,
        string Title,
        string State,
        IReadOnlyList<string> Labels
    );

    internal sealed record Response(ConnectorView? Connector, IReadOnlyList<StoryView> Stories);

    internal sealed record Query(Guid ProjectId) : IQuery<Response>;

    internal sealed class Handler(BacklogDbContext database) : IAppQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            var connector = await database
                .Connectors.Where(entity => entity.ProjectId == query.ProjectId)
                .Select(entity => new ConnectorView(
                    entity.Vendor.ToString(),
                    entity.Owner,
                    entity.Repository,
                    entity.SecretName,
                    entity.SecretSetAt,
                    entity.CodeRepository,
                    entity.LastSyncedAt,
                    entity.LastFailure,
                    entity.LastFailureAt
                ))
                .FirstOrDefaultAsync(cancellationToken);

            var stories = await database
                .Stories.Where(entity => entity.ProjectId == query.ProjectId)
                .OrderBy(entity => entity.VendorId)
                .Select(entity => new StoryView(
                    entity.VendorId,
                    entity.Title,
                    entity.State,
                    entity.Labels
                ))
                .ToListAsync(cancellationToken);

            return new Response(connector, stories);
        }
    }
}
