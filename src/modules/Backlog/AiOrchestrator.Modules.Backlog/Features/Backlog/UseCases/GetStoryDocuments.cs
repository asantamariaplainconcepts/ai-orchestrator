using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Domain;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog.UseCases;

/// <summary>
/// UC-023 — the documents written for a Story. Read live through the Connector at the change's
/// head ref, never mirrored (design D3): the Mirror exists for what is polled and matched, and
/// a document has no cache to invalidate if there is no cache.
/// <para>
/// Three absences stay three facts (design D5): no linked change, a change with no documents,
/// and a read that failed each have a different next action.
/// </para>
/// </summary>
sealed class GetStoryDocuments : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/backlog/stories/{vendorStoryId}/documents",
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
            .WithName(nameof(GetStoryDocuments))
            .WithTags("Backlog");

        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/backlog/stories/{vendorStoryId}/documents/content",
                async (
                    Guid projectId,
                    string vendorStoryId,
                    string path,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new ContentQuery(projectId, vendorStoryId, path),
                        cancellationToken
                    );
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .WithName("GetStoryDocumentContent")
            .WithTags("Backlog");
    }

    /// <summary>Null <see cref="Change"/> means no change references this Story — not a failure.</summary>
    internal sealed record Response(ChangeView? Change, IReadOnlyList<string> Documents);

    internal sealed record ChangeView(int Number, string Title, string Url, string HeadRef);

    internal sealed record ContentResponse(string Path, string HeadRef, string Content);

    [Requires(BacklogPermissions.Read)]
    internal sealed record Query(Guid ProjectId, string VendorStoryId)
        : IQuery<ErrorOr<Response>>,
            IScopedToProject;

    [Requires(BacklogPermissions.Read)]
    internal sealed record ContentQuery(Guid ProjectId, string VendorStoryId, string Path)
        : IQuery<ErrorOr<ContentResponse>>,
            IScopedToProject;

    internal sealed class Handler(ConnectorAccess access)
        : IAppQueryHandler<Query, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Query query,
            CancellationToken cancellationToken
        )
        {
            var context = await access.Resolve(query.ProjectId, cancellationToken);
            if (context.IsError)
            {
                return context.Errors;
            }

            var (connector, coordinates, token) = context.Value;

            var change = await connector.FindLinkedChange(
                coordinates,
                query.VendorStoryId,
                token,
                cancellationToken
            );
            if (change.IsError)
            {
                return change.Errors;
            }

            if (change.Value is null)
            {
                return new Response(Change: null, Documents: []);
            }

            var files = await connector.ListChangeFiles(
                coordinates,
                change.Value.Number,
                token,
                cancellationToken
            );

            // The documents list is the markdown projection of the files list (design D1):
            // one vendor call, two consumers. Deletions are not documents.
            return files.IsError
                ? files.Errors
                : new Response(
                    new ChangeView(
                        change.Value.Number,
                        change.Value.Title,
                        change.Value.Url,
                        change.Value.HeadRef
                    ),
                    [
                        .. files
                            .Value.Where(file =>
                                !string.Equals(file.Status, "removed", StringComparison.Ordinal)
                                && file.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                            )
                            .Select(file => file.Path),
                    ]
                );
        }
    }

    internal sealed class ContentHandler(ConnectorAccess access)
        : IAppQueryHandler<ContentQuery, ErrorOr<ContentResponse>>
    {
        public async Task<ErrorOr<ContentResponse>> Handle(
            ContentQuery query,
            CancellationToken cancellationToken
        )
        {
            var context = await access.Resolve(query.ProjectId, cancellationToken);
            if (context.IsError)
            {
                return context.Errors;
            }

            var (connector, coordinates, token) = context.Value;

            var change = await connector.FindLinkedChange(
                coordinates,
                query.VendorStoryId,
                token,
                cancellationToken
            );
            if (change.IsError)
            {
                return change.Errors;
            }

            if (change.Value is null)
            {
                return BacklogErrors.DocumentNotFound(query.Path);
            }

            // Read at the change's head, which is what makes "the branch moved on" correct by
            // construction rather than by an expiry policy.
            var content = await connector.ReadDocument(
                coordinates,
                query.Path,
                change.Value.HeadRef,
                token,
                cancellationToken
            );

            return content.IsError
                ? content.Errors
                : new ContentResponse(query.Path, change.Value.HeadRef, content.Value);
        }
    }
}
