using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Domain;
using AiOrchestrator.Modules.Backlog.Persistence;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog.UseCases;

/// <summary>
/// UC-022 — one Story with its description. Its own endpoint rather than a wider backlog list
/// (design D4): a body per row would inflate every backlog read for data one row at a time
/// actually needs. The body is returned verbatim — sanitising is the renderer's job (D2).
/// </summary>
sealed class GetStory : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/backlog/stories/{vendorStoryId}",
                async (
                    Guid projectId,
                    string vendorStoryId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Query(projectId, vendorStoryId),
                        cancellationToken
                    );
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .WithName(nameof(GetStory))
            .WithTags("Backlog");

    internal sealed record Response(
        string VendorId,
        string Title,
        string State,
        IReadOnlyList<string> Labels,
        string? Body,
        DateTimeOffset LastSeenAt
    );

    [Requires(BacklogPermissions.Read)]
    internal sealed record Query(Guid ProjectId, string VendorStoryId)
        : IQuery<ErrorOr<Response>>,
            IScopedToProject;

    internal sealed class Handler(BacklogDbContext database)
        : IAppQueryHandler<Query, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Query query,
            CancellationToken cancellationToken
        )
        {
            var story = await database
                .Stories.Where(entity =>
                    entity.ProjectId == query.ProjectId && entity.VendorId == query.VendorStoryId
                )
                .Select(entity => new Response(
                    entity.VendorId,
                    entity.Title,
                    entity.State,
                    entity.Labels,
                    entity.Body,
                    entity.LastSeenAt
                ))
                .FirstOrDefaultAsync(cancellationToken);

            return story is null ? BacklogErrors.StoryNotFound(query.VendorStoryId) : story;
        }
    }
}
