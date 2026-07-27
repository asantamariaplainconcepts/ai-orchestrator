using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Features.Observation.UseCases;

/// <summary>
/// What the project's agents have cost — the number the experiment is judged by (DEC-038).
/// <para>
/// Summed over Runs that actually reported, with the unreported ones counted separately
/// (design D2): folding nulls in as zero would understate the total quietly and permanently,
/// and a reader could never tell "cheap" from "unmeasured".
/// </para>
/// </summary>
sealed class GetProjectCost : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/runs/cost",
                async (Guid projectId, ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new Query(projectId), cancellationToken))
            )
            .WithName(nameof(GetProjectCost))
            .WithTags("Runs");

    internal sealed record Response(
        decimal TotalCostUsd,
        long TotalInputTokens,
        long TotalOutputTokens,
        int ReportedRuns,
        int UnknownRuns
    );

    internal sealed record Query(Guid ProjectId) : IQuery<Response>;

    internal sealed class Handler(RunsDbContext database) : IAppQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            var runs = database.Runs.Where(run => run.ProjectId == query.ProjectId);

            // Aggregated in SQL rather than by pulling every Run to the client (design D3) —
            // still correct when the table is paged, which it eventually will be.
            var reported = await runs.Where(run => run.CostUsd != null)
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    Cost = group.Sum(run => run.CostUsd!.Value),
                    Input = group.Sum(run => run.UsageInputTokens ?? 0),
                    Output = group.Sum(run => run.UsageOutputTokens ?? 0),
                    Count = group.Count(),
                })
                .FirstOrDefaultAsync(cancellationToken);

            var unknown = await runs.CountAsync(run => run.CostUsd == null, cancellationToken);

            return new Response(
                reported?.Cost ?? 0m,
                reported?.Input ?? 0,
                reported?.Output ?? 0,
                reported?.Count ?? 0,
                unknown
            );
        }
    }
}
