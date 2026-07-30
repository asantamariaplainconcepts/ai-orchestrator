using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Dispatch;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Runs.Features.Approval.UseCases;

/// <summary>
/// UC-013 — the human half of the gate. Approving stamps the Run and re-enqueues it, which is
/// all phase 2 needs to route itself (approval-gate D1); rejecting ends it `Cancelled`, which
/// is terminal and frees the Story (D5, BR-001/BR-012).
/// <para>
/// No approver identity is recorded: authentication does not exist yet (#11–#13). That is a
/// stated gap, not an oversight — #13 attaches the permission and the name.
/// </para>
/// </summary>
sealed class DecideOnPlan : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/runs/{runId:guid}/approve",
                async (
                    Guid projectId,
                    Guid runId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Command(projectId, runId, Approve: true),
                        cancellationToken
                    );
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .WithName("ApprovePlan")
            .WithTags("Runs");

        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/runs/{runId:guid}/reject",
                async (
                    Guid projectId,
                    Guid runId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Command(projectId, runId, Approve: false),
                        cancellationToken
                    );
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .WithName("RejectPlan")
            .WithTags("Runs");
    }

    internal sealed record Response(Guid Id, string State);

    [Requires(Access.MemberOfProject)]
    internal sealed record Command(Guid ProjectId, Guid RunId, bool Approve)
        : ICommand<ErrorOr<Response>>,
            IScopedToProject;

    internal sealed class Handler(
        RunsDbContext database,
        IRunDispatcher dispatcher,
        TimeProvider clock
    ) : IAppCommandHandler<Command, ErrorOr<Response>>
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

            if (run.State != RunState.AwaitingApproval)
            {
                // A decision on a Run that is not waiting is a question with no answer —
                // saying which state it is in beats a generic refusal.
                return RunsErrors.RunNotAwaitingApproval(command.RunId, run.State.ToString());
            }

            if (!command.Approve)
            {
                run.Reject(clock.GetUtcNow());
                await database.SaveChangesAsync(cancellationToken);
                return new Response(run.Id, run.State.ToString());
            }

            run.Approve(clock.GetUtcNow());
            await database.SaveChangesAsync(cancellationToken);

            // Commit first, then enqueue — the same ordering, and the same visible crash
            // window, as creation (run-orchestration D4). A stamped Queued Run with no message
            // is recoverable by Run now.
            await dispatcher.Dispatch(run.Id, cancellationToken);

            return new Response(run.Id, run.State.ToString());
        }
    }
}
