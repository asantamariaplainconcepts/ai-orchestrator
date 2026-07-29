using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Features.Observation.UseCases;

/// <summary>
/// #145 — the decision UC-026 could not express: this failure needs no re-run.
/// <para>
/// Stored rather than derived, and that is an addition to #94's design D2 rather than a
/// contradiction of it. That change was right that "a newer Run exists" must be a query, because
/// BR-013's two re-trigger paths would each forget a flag. But nothing in the data distinguishes
/// "nobody has decided yet" from "somebody decided not to act" — those are identical rows, so a
/// query cannot derive the second. Derived facts are about the world; stored facts are about a
/// person.
/// </para>
/// <para>
/// The Run stays <c>Failed</c>. A dismissal records that somebody looked, never that the Run
/// succeeded — changing the state to clear a list would be rewriting history (BR-014), and BR-004
/// means nothing re-runs either way.
/// </para>
/// <para>
/// No dismisser identity: authentication does not exist yet (OPN-002), and storing "anonymous"
/// would read as an answer. The time is recorded because that much is true.
/// </para>
/// </summary>
sealed class DismissFailure : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/runs/{runId:guid}/dismiss",
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
            .WithName(nameof(DismissFailure))
            .WithTags("Runs");

    internal sealed record Response(Guid RunId, DateTimeOffset DismissedAt);

    internal sealed record Command(Guid ProjectId, Guid RunId) : ICommand<ErrorOr<Response>>;

    internal sealed class Handler(RunsDbContext database, TimeProvider clock)
        : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var run = await database.Runs.FirstOrDefaultAsync(
                entity => entity.Id == command.RunId && entity.ProjectId == command.ProjectId,
                cancellationToken
            );

            if (run is null)
            {
                return RunsErrors.RunNotFound(command.RunId);
            }

            // Only a failure is a failure to dismiss. Refusing rather than ignoring, because a
            // caller dismissing a running Run has misunderstood something and should hear so.
            if (!run.Dismiss(clock.GetUtcNow()))
            {
                return RunsErrors.NotAFailure(command.RunId, run.State.ToString());
            }

            await database.SaveChangesAsync(cancellationToken);

            return new Response(run.Id, run.DismissedAt!.Value);
        }
    }
}
