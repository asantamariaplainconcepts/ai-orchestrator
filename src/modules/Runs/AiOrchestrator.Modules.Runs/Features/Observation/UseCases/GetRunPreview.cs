using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Features.Observation.UseCases;

/// <summary>
/// Whether this Run has a preview to look at <b>right now</b> (run-previews). The live sibling of
/// <see cref="GetRunLog"/>, and it answers the same way: from the Run's own state and the
/// machine's own record, never from a stored field.
/// <para>
/// That is what makes "a finished Run offers nothing" a property rather than a branch somebody
/// has to remember. A terminal Run has no entry in the ledger — the launcher removed it when the
/// sandbox went — and terminality is read from <see cref="RunStates.IsTerminal"/> rather than a
/// hand-written list, because such copies have drifted twice in this codebase already.
/// </para>
/// </summary>
sealed class GetRunPreview : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/runs/{runId:guid}/preview",
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
            .WithName(nameof(GetRunPreview))
            .WithTags("Runs");

    [Requires(RunPermissions.Read)]
    internal sealed record Query(Guid ProjectId, Guid RunId) : IQuery<Response?>, IScopedToProject;

    /// <summary>
    /// <paramref name="Hosted"/> is the habitat's answer and <paramref name="Available"/> is this
    /// Run's. They are separate because "previews are not hosted here" and "this Run has no
    /// preview" are different sentences: a portal that is not the sandbox host would otherwise
    /// read as the Run having failed to make one.
    /// </summary>
    internal sealed record Response(bool Hosted, bool Available);

    internal sealed class Handler(RunsDbContext database, IRunPreviewMonitor previews)
        : IAppQueryHandler<Query, Response?>
    {
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

            // Both conditions, and either one alone is not enough: a ledger entry for a Run that
            // has since gone terminal would be a window onto a sandbox that is being disposed,
            // and a live Run with no entry simply never published one.
            var available =
                previews.Hosted
                && !RunStates.IsTerminal(run.State)
                && previews.PortFor(query.RunId) is not null;

            return new Response(previews.Hosted, available);
        }
    }
}
