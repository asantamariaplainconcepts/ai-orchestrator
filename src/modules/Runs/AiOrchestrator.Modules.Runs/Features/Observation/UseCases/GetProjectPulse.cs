using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Projects.Contracts;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Features.Observation.UseCases;

/// <summary>
/// #108 — the project's pulse over a seven-day window, computed at read time from data BR-014
/// already forces every Run to carry. Derived, never stored (design D1, the inbox's shape):
/// the mirror keeps no history, so this describes the runs that exist, honestly.
/// <para>
/// Every figure is derivable by hand from the run list (design D2): the tests recompute each
/// one with the same arithmetic a human would use. Cost follows BR-011 exactly as project cost
/// does — sum the known, state how many were excluded; unknown is never zero.
/// </para>
/// </summary>
sealed class GetProjectPulse : IUseCase
{
    static readonly TimeSpan Window = TimeSpan.FromDays(7);

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/pulse",
                async (Guid projectId, ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new Query(projectId), cancellationToken))
            )
            .WithName(nameof(GetProjectPulse))
            .WithTags("Runs");

    [Requires(Access.MemberOfProject)]
    internal sealed record Query(Guid ProjectId) : IQuery<Response>, IScopedToProject;

    /// <summary>An automation with zero window runs still appears — absence is the signal.</summary>
    internal sealed record AutomationEntry(
        Guid AutomationId,
        string TriggerLabel,
        string Action,
        int Fired,
        int Failed
    );

    /// <summary>The inbox's reason vocabulary, scoped to the project and counted.</summary>
    internal sealed record Waiting(int Approval, int Input, int Failure);

    internal sealed record Response(
        int RunsStarted,
        int TerminalRuns,
        double? SuccessRate,
        decimal KnownCostUsd,
        int ReportedRuns,
        int UnknownCostRuns,
        double? MeanQueueWaitSeconds,
        double? MeanDurationSeconds,
        IReadOnlyList<AutomationEntry> Automations,
        int StoriesTotal,
        int StoriesNeverRun,
        Waiting Waiting,
        double? OldestOpenQuestionSeconds
    );

    internal sealed class Handler(
        RunsDbContext database,
        IAutomationCatalog automations,
        IStoryReader stories,
        TimeProvider clock
    ) : IAppQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            var now = clock.GetUtcNow();
            var windowStart = now - Window;

            // Materialised once: the window is human-scale by construction, and every figure
            // below is plain arithmetic over the same list a Member can read (design D2).
            var windowRuns = await database
                .Runs.Where(run => run.ProjectId == query.ProjectId && run.CreatedAt >= windowStart)
                .ToListAsync(cancellationToken);

            var terminal = windowRuns
                .Where(run =>
                    run.State is RunState.Succeeded or RunState.Failed or RunState.Cancelled
                )
                .ToList();
            var reported = windowRuns.Where(run => run.CostUsd != null).ToList();

            var queueWaits = windowRuns
                .Where(run => run.DispatchedAt != null && run.StartedAt != null)
                .Select(run => (run.StartedAt!.Value - run.DispatchedAt!.Value).TotalSeconds)
                .ToList();
            var durations = windowRuns
                .Where(run => run.StartedAt != null && run.EndedAt != null)
                .Select(run => (run.EndedAt!.Value - run.StartedAt!.Value).TotalSeconds)
                .ToList();

            return new Response(
                windowRuns.Count,
                terminal.Count,
                terminal.Count == 0
                    ? null
                    : terminal.Count(run => run.State == RunState.Succeeded)
                        / (double)terminal.Count,
                reported.Sum(run => run.CostUsd!.Value),
                reported.Count,
                windowRuns.Count - reported.Count,
                queueWaits.Count == 0 ? null : queueWaits.Average(),
                durations.Count == 0 ? null : durations.Average(),
                await AutomationEntries(query.ProjectId, windowRuns, cancellationToken),
                await StoriesTotal(query.ProjectId, cancellationToken),
                await StoriesNeverRun(query.ProjectId, cancellationToken),
                await ProjectWaiting(query.ProjectId, cancellationToken),
                await OldestOpenQuestion(query.ProjectId, now, cancellationToken)
            );
        }

        /// <summary>
        /// Enabled automations first — zero-run ones included, because an automation nobody
        /// triggers is the row #84's delete made actionable — then any automation the window
        /// actually fired that is disabled today (Detail deliberately ignores Enabled).
        /// </summary>
        async Task<IReadOnlyList<AutomationEntry>> AutomationEntries(
            Guid projectId,
            List<Run> windowRuns,
            CancellationToken cancellationToken
        )
        {
            var byAutomation = windowRuns
                .GroupBy(run => run.AutomationId)
                .ToDictionary(
                    group => group.Key,
                    group =>
                        (
                            Fired: group.Count(),
                            Failed: group.Count(run => run.State == RunState.Failed)
                        )
                );

            var entries = new List<AutomationEntry>();

            foreach (
                var trigger in await automations.EnabledAutomations(projectId, cancellationToken)
            )
            {
                var detail = await automations.Detail(
                    projectId,
                    trigger.AutomationId,
                    cancellationToken
                );
                var counts = byAutomation.GetValueOrDefault(trigger.AutomationId);
                entries.Add(
                    new AutomationEntry(
                        trigger.AutomationId,
                        trigger.TriggerLabel,
                        detail?.Action ?? string.Empty,
                        counts.Fired,
                        counts.Failed
                    )
                );
                byAutomation.Remove(trigger.AutomationId);
            }

            foreach (var (automationId, counts) in byAutomation)
            {
                var detail = await automations.Detail(projectId, automationId, cancellationToken);
                if (detail is null)
                {
                    continue; // Deleted automations cannot have Runs (#84); nothing to report.
                }

                entries.Add(
                    new AutomationEntry(
                        automationId,
                        detail.TriggerLabel,
                        detail.Action,
                        counts.Fired,
                        counts.Failed
                    )
                );
            }

            return entries;
        }

        async Task<int> StoriesTotal(Guid projectId, CancellationToken cancellationToken) =>
            (await stories.VendorStoryIds(projectId, cancellationToken)).Count;

        /// <summary>All-time coverage: a story run last month is covered, just not recently.</summary>
        async Task<int> StoriesNeverRun(Guid projectId, CancellationToken cancellationToken)
        {
            var storyIds = await stories.VendorStoryIds(projectId, cancellationToken);
            var everRun = await database
                .Runs.Where(run => run.ProjectId == projectId)
                .Select(run => run.VendorStoryId)
                .Distinct()
                .ToListAsync(cancellationToken);

            return storyIds.Except(everRun, StringComparer.Ordinal).Count();
        }

        /// <summary>
        /// The inbox's predicate, scoped and counted. They cannot disagree any more: both call
        /// <see cref="WaitingRuns.WaitingOnAHuman"/>, where a comment used to have to promise it.
        /// </summary>
        async Task<Waiting> ProjectWaiting(Guid projectId, CancellationToken cancellationToken)
        {
            // The inbox's predicate, not a copy of it (#145, design D5). Scoping after the shared
            // filter is safe: the newer-Run test needs the whole set, and SQL reorders the rest.
            var waiting = await database
                .Runs.WaitingOnAHuman()
                .Where(run => run.ProjectId == projectId)
                .GroupBy(run => run.State)
                .Select(group => new { group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);

            return new Waiting(
                waiting.SingleOrDefault(entry => entry.Key == RunState.AwaitingApproval)?.Count
                    ?? 0,
                waiting.SingleOrDefault(entry => entry.Key == RunState.AwaitingInput)?.Count ?? 0,
                waiting.SingleOrDefault(entry => entry.Key == RunState.Failed)?.Count ?? 0
            );
        }

        /// <summary>BR-006: waits are untimed, so their cost is made visible instead.</summary>
        async Task<double?> OldestOpenQuestion(
            Guid projectId,
            DateTimeOffset now,
            CancellationToken cancellationToken
        )
        {
            var oldest = await database
                .Runs.Where(run =>
                    run.ProjectId == projectId
                    && run.State == RunState.AwaitingInput
                    && run.WaitingSince != null
                )
                .MinAsync(run => run.WaitingSince, cancellationToken);

            return oldest is null ? null : (now - oldest.Value).TotalSeconds;
        }
    }
}
