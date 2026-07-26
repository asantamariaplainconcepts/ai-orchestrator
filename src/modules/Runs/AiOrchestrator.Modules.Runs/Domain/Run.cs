using AiOrchestrator.BuildingBlocks.Domain;

namespace AiOrchestrator.Modules.Runs.Domain;

/// <summary>
/// One execution of an Automation against a Story (BC-003). Born from a matched story event,
/// recorded per BR-014's subset: story reference, Automation, timestamps, state.
/// <para>
/// The story reference is (ProjectId, VendorStoryId) — the same identity the event and the
/// mirror use. BR-001's "one active Run per Story" is a partial unique index over it, not a
/// handler convention.
/// </para>
/// </summary>
sealed class Run : Aggregate
{
    Run() { }

    Run(Guid projectId, string vendorStoryId, Guid automationId, DateTimeOffset createdAt)
    {
        ProjectId = projectId;
        VendorStoryId = vendorStoryId;
        AutomationId = automationId;
        State = RunState.Queued;
        CreatedAt = createdAt;
    }

    public Guid ProjectId { get; private set; }

    public string VendorStoryId { get; private set; } = string.Empty;

    public Guid AutomationId { get; private set; }

    public RunState State { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// When the Run id was enqueued, or null. A `Queued` Run with a null value older than
    /// moments is the design-D4 crash window made visible — the row a human can find.
    /// </summary>
    public DateTimeOffset? DispatchedAt { get; private set; }

    public static Run Create(
        Guid projectId,
        string vendorStoryId,
        Guid automationId,
        DateTimeOffset createdAt
    ) => new(projectId, vendorStoryId, automationId, createdAt);

    public void MarkDispatched(DateTimeOffset at) => DispatchedAt = at;
}

/// <summary>
/// Every state here is <i>active</i> in BR-001's sense. Terminal states arrive with the issue
/// that lets a Run complete; the BR-001 index filter must be revisited when they do.
/// </summary>
enum RunState
{
    Queued = 1,
    Planning = 2,
    AwaitingApproval = 3,
    Executing = 4,
}
