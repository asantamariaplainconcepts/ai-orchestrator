using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Features.Observation.UseCases;

/// <summary>
/// UC-021 — dispatched work is observable. Strictly read (design D4): the response is exactly
/// the BR-014 subset the Run records today; automation details are the frontend's client-side
/// join, and DEC-031 fields with no producer yet are the UI's empty values, not fields here.
/// </summary>
sealed class ListRuns : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/runs",
                async (
                    Guid projectId,
                    string? vendorStoryId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                    Results.Ok(
                        await sender.Send(new Query(projectId, vendorStoryId), cancellationToken)
                    )
            )
            .WithName(nameof(ListRuns))
            .WithTags("Runs");

    [Requires(RunPermissions.Read)]
    internal sealed record Query(Guid ProjectId, string? VendorStoryId)
        : IQuery<IReadOnlyList<Response>>,
            IScopedToProject;

    internal sealed record Response(
        Guid Id,
        string VendorStoryId,
        Guid AutomationId,
        string State,
        DateTimeOffset CreatedAt,
        DateTimeOffset? DispatchedAt,
        string? OutputLink,
        string? Plan,
        DateTimeOffset? ApprovedAt,
        string? FailureReason,
        // Null means the runtime reported nothing (BR-011). Zero means it reported zero —
        // a real value since #30's free models, and the two must never render alike.
        long? InputTokens,
        long? OutputTokens,
        decimal? CostUsd,
        /// <summary>When a human decided this failure needs no re-run (#145); null until they do.</summary>
        DateTimeOffset? DismissedAt
    );

    internal sealed class Handler(RunsDbContext database)
        : IAppQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<IReadOnlyList<Response>> Handle(
            Query query,
            CancellationToken cancellationToken
        )
        {
            var runs = database.Runs.Where(run => run.ProjectId == query.ProjectId);

            if (!string.IsNullOrEmpty(query.VendorStoryId))
            {
                runs = runs.Where(run => run.VendorStoryId == query.VendorStoryId);
            }

            // Newest first; the v7 id is time-ordered, so it tiebreaks equal timestamps
            // without a second clock column (design D3).
            var ordered = await runs.OrderByDescending(run => run.CreatedAt)
                .ThenByDescending(run => run.Id)
                .ToListAsync(cancellationToken);

            // Materialise, then project: the response carries the state *name*, and ToString()
            // inside an EF projection is translated to SQL rather than evaluated in .NET.
            return
            [
                .. ordered.Select(run => new Response(
                    run.Id,
                    run.VendorStoryId,
                    run.AutomationId,
                    run.State.ToString(),
                    run.CreatedAt,
                    run.DispatchedAt,
                    run.OutputLink,
                    run.Plan,
                    run.ApprovedAt,
                    run.FailureReason,
                    run.UsageInputTokens,
                    run.UsageOutputTokens,
                    run.CostUsd,
                    run.DismissedAt
                )),
            ];
        }
    }
}
