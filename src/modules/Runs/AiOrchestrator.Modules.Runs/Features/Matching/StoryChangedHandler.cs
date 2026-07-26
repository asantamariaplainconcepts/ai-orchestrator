using AiOrchestrator.BuildingBlocks.IntegrationEvents;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Projects.Contracts;
using Microsoft.Extensions.Logging;

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
/// Creation itself lives in <see cref="RunCreator"/> — the same path Run now takes (BR-013) —
/// and every non-created outcome is deliberately silent here: nobody asked a question.
/// </para>
/// </summary>
sealed class StoryChangedHandler(
    IStoryReader stories,
    IAutomationCatalog automations,
    RunCreator creator
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

        await creator.Create(@event.ProjectId, @event.VendorStoryId, match, cancellationToken);
    }

    static bool Matches(AutomationTrigger trigger, StorySnapshot story) =>
        story.Labels.Contains(trigger.TriggerLabel, StringComparer.Ordinal)
        && (
            trigger.TriggerState is null
            || string.Equals(trigger.TriggerState, story.State, StringComparison.Ordinal)
        );
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
