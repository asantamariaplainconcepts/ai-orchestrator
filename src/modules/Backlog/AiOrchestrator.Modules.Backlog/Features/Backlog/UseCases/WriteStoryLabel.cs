using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog.UseCases;

/// <summary>
/// UC-008 — a Member applies or removes a label from the portal. PUT and DELETE on the label
/// resource, both idempotent (the vendor's add-to-set / remove-of-absent semantics carry
/// through, design D3). The response is the refresh response: by the time this returns, the
/// mirror has been re-synchronised through the ordinary path.
/// </summary>
sealed class WriteStoryLabel : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPut(
                "/api/projects/{projectId:guid}/backlog/stories/{vendorStoryId}/labels/{label}",
                async (
                    Guid projectId,
                    string vendorStoryId,
                    string label,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Command(projectId, vendorStoryId, label, Apply: true),
                        cancellationToken
                    );
                    return result.Match(response => Results.Ok(response), ApiResults.Problem);
                }
            )
            .WithName("ApplyStoryLabel")
            .WithTags("Backlog");

        endpoints
            .MapDelete(
                "/api/projects/{projectId:guid}/backlog/stories/{vendorStoryId}/labels/{label}",
                async (
                    Guid projectId,
                    string vendorStoryId,
                    string label,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Command(projectId, vendorStoryId, label, Apply: false),
                        cancellationToken
                    );
                    return result.Match(response => Results.Ok(response), ApiResults.Problem);
                }
            )
            .WithName("RemoveStoryLabel")
            .WithTags("Backlog");
    }

    internal sealed record Response(int Changes);

    [Requires(BacklogPermissions.WriteLabel)]
    internal sealed record Command(Guid ProjectId, string VendorStoryId, string Label, bool Apply)
        : ICommand<ErrorOr<Response>>,
            IScopedToProject;

    internal sealed class Handler(LabelWriteBack writeBack)
        : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var result = command.Apply
                ? await writeBack.Apply(
                    command.ProjectId,
                    command.VendorStoryId,
                    command.Label,
                    cancellationToken
                )
                : await writeBack.Remove(
                    command.ProjectId,
                    command.VendorStoryId,
                    command.Label,
                    cancellationToken
                );

            return result.Match<ErrorOr<Response>>(
                changes => new Response(changes),
                errors => errors
            );
        }
    }
}
