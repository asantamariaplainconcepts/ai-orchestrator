using AiOrchestrator.BuildingBlocks.Dispatch;
using AiOrchestrator.BuildingBlocks.IntegrationEvents;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Projects.Contracts;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AiOrchestrator.Modules.Runs.Features.Matching;

/// <summary>
/// UC-011 — the loop closes here: a story event, matched against the Project's Automations,
/// becomes a Run and a dispatch message.
/// <para>
/// The event is only a pointer. Matching reads the Story's <i>current</i> labels and state
/// through Contracts (BR-015, design D2): a superseded change matches against the newer truth,
/// which is correct by BR-008, and a Story deleted in the meantime reads as absent.
/// </para>
/// <para>
/// Delivery is at-least-once. Idempotency is BR-001's partial unique index, not a message
/// ledger (design D3): the second identical delivery loses the insert and reports success.
/// </para>
/// </summary>
sealed class StoryChangedHandler(
    RunsDbContext database,
    IStoryReader stories,
    IAutomationCatalog automations,
    IRunDispatcher dispatcher,
    RunsOptions options,
    TimeProvider clock,
    ILogger<StoryChangedHandler> logger
) : IIntegrationEventHandler<StoryChanged>
{
    public async Task Handle(StoryChanged @event, CancellationToken cancellationToken)
    {
        // A removed Story has nothing to run against — Removed never matches.
        if (@event.Kind == StoryChangeKind.Removed)
        {
            return;
        }

        var story = await stories.Find(@event.ProjectId, @event.VendorStoryId, cancellationToken);
        if (story is null)
        {
            return;
        }

        var candidates = await automations.EnabledAutomations(@event.ProjectId, cancellationToken);

        // BR-003 guarantees at most one enabled Automation matches; saving enforced it.
        var match = candidates.FirstOrDefault(candidate => Matches(candidate, story));
        if (match is null)
        {
            return;
        }

        if (match.RequiresApproval)
        {
            // This slice's stated limitation (design D6): the two-phase lane is its own issue,
            // and parking a Run now would freeze approval semantics before it is designed.
            MatchingLog.TwoPhaseRefused(logger, match.AutomationId);
            return;
        }

        // BR-001 pre-check keeps the common case quiet; the index owns the race below.
        var hasActiveRun = await database.Runs.AnyAsync(
            run => run.ProjectId == @event.ProjectId && run.VendorStoryId == @event.VendorStoryId,
            cancellationToken
        );
        if (hasActiveRun)
        {
            return;
        }

        // BR-002, creation-side: at the cap the Run waits Queued and nothing is enqueued.
        var busy = await database.Runs.CountAsync(
            run =>
                run.ProjectId == @event.ProjectId
                && (run.State == RunState.Planning || run.State == RunState.Executing),
            cancellationToken
        );
        var belowCap = busy < options.ProjectConcurrencyCap;

        var run = Run.Create(
            @event.ProjectId,
            @event.VendorStoryId,
            match.AutomationId,
            clock.GetUtcNow()
        );
        database.Runs.Add(run);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateActiveRun(exception))
        {
            // A concurrent delivery for the same Story won the insert. BR-001 says ignored,
            // not queued — the loser reports success and enqueues nothing.
            return;
        }

        if (!belowCap)
        {
            MatchingLog.QueuedAtCap(logger, run.Id, @event.ProjectId);
            return;
        }

        try
        {
            await dispatcher.Dispatch(run.Id, cancellationToken);
        }
        catch (Exception exception)
        {
            // Design D4's crash window, logged loudly rather than retried: a CAP retry of this
            // handler would find the active Run and return without dispatching, so propagating
            // buys nothing. The Run stays Queued and visible; Run now (BR-013) is the recovery.
            MatchingLog.DispatchFailed(logger, exception, run.Id);
            return;
        }

        run.MarkDispatched(clock.GetUtcNow());
        await database.SaveChangesAsync(cancellationToken);
    }

    static bool Matches(AutomationTrigger trigger, StorySnapshot story) =>
        story.Labels.Contains(trigger.TriggerLabel, StringComparer.Ordinal)
        && (
            trigger.TriggerState is null
            || string.Equals(trigger.TriggerState, story.State, StringComparison.Ordinal)
        );

    // Narrow on purpose, same as the Backlog reconciler: only a unique-key violation means
    // "someone else already did this".
    static bool IsDuplicateActiveRun(DbUpdateException exception) =>
        exception.InnerException
            is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

static partial class MatchingLog
{
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Warning,
        Message = "Automation {AutomationId} matched but requires approval — the two-phase lane is not implemented yet, no Run was created"
    )]
    public static partial void TwoPhaseRefused(ILogger logger, Guid automationId);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "Run {RunId} created Queued: project {ProjectId} is at its concurrency cap (BR-002)"
    )]
    public static partial void QueuedAtCap(ILogger logger, Guid runId, Guid projectId);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Error,
        Message = "Run {RunId} was created but could not be enqueued — it remains Queued with no message; re-trigger manually"
    )]
    public static partial void DispatchFailed(ILogger logger, Exception exception, Guid runId);
}
