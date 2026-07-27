using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Runs.Features.Approval.UseCases;

/// <summary>
/// UC-014 — stopping a Run. The state is written immediately (design D1): the Story frees now
/// rather than when a worker acknowledges, and the UI stops implying work that has been called
/// off. The worker cooperates at its own boundaries, so nothing this Run started gets published.
/// </summary>
sealed class CancelRun : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/runs/{runId:guid}/cancel",
                async (
                    Guid projectId,
                    Guid runId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Command(projectId, runId),
                        cancellationToken
                    );
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .WithName(nameof(CancelRun))
            .WithTags("Runs");

    internal sealed record Response(Guid Id, string State);

    internal sealed record Command(Guid ProjectId, Guid RunId) : ICommand<ErrorOr<Response>>;

    internal sealed class Handler(RunsDbContext database, TimeProvider clock)
        : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var run = await database.Runs.FindAsync([command.RunId], cancellationToken);

            if (run is null || run.ProjectId != command.ProjectId)
            {
                return RunsErrors.RunNotFound(command.RunId);
            }

            if (!RunStates.Active.Contains(run.State))
            {
                return RunsErrors.RunAlreadyFinished(command.RunId, run.State.ToString());
            }

            run.Cancel(clock.GetUtcNow());
            await database.SaveChangesAsync(cancellationToken);

            return new Response(run.Id, run.State.ToString());
        }
    }
}
