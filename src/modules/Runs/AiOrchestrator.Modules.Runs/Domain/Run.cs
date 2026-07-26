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

    /// <summary>When execution actually began — the claim, not the enqueue (BR-014).</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>Why a Run failed, in one honest sentence. Null on success.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>BR-011/DEC-038: all three null together means "unknown" — never invented.</summary>
    public long? UsageInputTokens { get; private set; }

    public long? UsageOutputTokens { get; private set; }

    public decimal? CostUsd { get; private set; }

    public void MarkExecuting(DateTimeOffset at)
    {
        State = RunState.Executing;
        StartedAt = at;
    }

    public void Succeed(DateTimeOffset at, long? inputTokens, long? outputTokens, decimal? costUsd)
    {
        State = RunState.Succeeded;
        EndedAt = at;
        UsageInputTokens = inputTokens;
        UsageOutputTokens = outputTokens;
        CostUsd = costUsd;
    }

    public void Fail(DateTimeOffset at, string reason)
    {
        State = RunState.Failed;
        EndedAt = at;
        FailureReason = reason;
    }
}

/// <summary>
/// The first four states are <i>active</i> in BR-001's sense and are the exact list the
/// partial unique index filters on. <see cref="Succeeded"/> and <see cref="Failed"/> are
/// terminal and deliberately outside that filter: a finished Story can run again, and a
/// Failed Run is re-run only by a human (BR-004).
/// </summary>
enum RunState
{
    Queued = 1,
    Planning = 2,
    AwaitingApproval = 3,
    Executing = 4,
    Succeeded = 5,
    Failed = 6,
}
