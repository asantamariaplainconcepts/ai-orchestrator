using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Projects.Domain;
using AiOrchestrator.Modules.Projects.Persistence;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Projects.Features.Projects.UseCases;

/// <summary>
/// The project's Story lifecycle, served by the aggregate that owns it (#310, ADR-0022). This
/// endpoint is the whole reason the six label walks collapse into one read: the owner serves the
/// order, so a client has nothing left to re-derive, and the board, the read-only preview and the
/// plan all read the same list instead of computing three answers that disagree.
/// <para>
/// Gated on <see cref="ProjectPermissions.ReadAutomations"/>, not on managing them: UC-007 has an
/// ACT-002 Member reading the board, and the board's columns <i>are</i> this list (BR-009 keeps them
/// from rearranging it, which is the write's business and not the read's). The list carries stage
/// names — vendor labels an Admin chose — and no credential, so there is nothing here a reader of
/// the backlog does not already see on the Stories themselves.
/// </para>
/// <para>
/// A read of its own rather than a field bolted onto the Automations list: the lifecycle belongs to
/// the Project, and serving it from the Automations collection would say, in the shape of the API,
/// exactly the thing this change stopped being true — that the flow is a property of the
/// Automations.
/// </para>
/// </summary>
sealed class GetLifecycle : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/lifecycle",
                async (Guid projectId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(projectId), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .WithName(nameof(GetLifecycle))
            .WithTags("Projects");

    /// <summary>
    /// The stages in the stored order — array position is the order, and this response preserves it.
    /// Empty is an ordinary answer: a project whose Automations have claimed nothing has no
    /// lifecycle yet, which is not a misconfiguration (seeding one is out of scope).
    /// </summary>
    internal sealed record Response(IReadOnlyList<string> Stages);

    [Requires(ProjectPermissions.ReadAutomations)]
    internal sealed record Query(Guid ProjectId) : IQuery<ErrorOr<Response>>, IScopedToProject;

    internal sealed class Handler(ProjectsDbContext database)
        : IAppQueryHandler<Query, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Query query,
            CancellationToken cancellationToken
        )
        {
            var stages = await database
                .Projects.Where(project => project.Id == query.ProjectId)
                .Select(project => project.LifecycleStages)
                .FirstOrDefaultAsync(cancellationToken);

            // Null distinguishes "no such project" from "a project with no stages yet" — the two
            // deserve different answers, and an empty list for both would make a 404 unreachable.
            return stages is null ? ProjectErrors.NotFound(query.ProjectId) : new Response(stages);
        }
    }
}
