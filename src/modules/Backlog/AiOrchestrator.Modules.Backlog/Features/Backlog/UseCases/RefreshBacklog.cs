using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Modules;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog.UseCases;

/// <summary>
/// UC-009's on-demand half — poll now rather than waiting for the interval.
/// <para>
/// This is also the path the tests drive: asserting on a background timer is the flakiest thing
/// we could add, so the deterministic entry point exercises exactly the same synchroniser the
/// timer uses.
/// </para>
/// </summary>
sealed class RefreshBacklog : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/backlog/refresh",
                async (Guid projectId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Command(projectId), cancellationToken);
                    return result.Match(response => Results.Ok(response), ApiResults.Problem);
                }
            )
            .WithName(nameof(RefreshBacklog))
            .WithTags("Backlog");

    internal sealed record Response(int Changes);

    internal sealed record Command(Guid ProjectId) : ICommand<ErrorOr<Response>>;

    internal sealed class Handler(BacklogSynchroniser synchroniser)
        : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var result = await synchroniser.Synchronise(command.ProjectId, cancellationToken);

            return result.Match<ErrorOr<Response>>(
                changes => new Response(changes),
                errors => errors
            );
        }
    }
}
