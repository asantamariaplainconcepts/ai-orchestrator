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

    /// <summary>
    /// Where the work landed, when the runtime reports it (BR-014). Since #162 the orchestrator no
    /// longer publishes anything itself, so this is filled only if the agent's own result names a
    /// destination — an accepted consequence of the agent doing the publishing.
    /// </summary>
    public string? OutputLink { get; private set; }

    /// <summary>The Agent's proposal, awaiting a human (UC-015). Null on the single-phase lane.</summary>
    public string? Plan { get; private set; }

    /// <summary>
    /// When a human approved the Plan. This stamp is also the phase router (design D1): an
    /// approval-gated Run without it gets the plan phase, everything else gets execution.
    /// </summary>
    public DateTimeOffset? ApprovedAt { get; private set; }

    /// <summary>
    /// When a human decided this failure needs no re-run (#145). Null means nobody has decided yet,
    /// and those two are genuinely different states that no query could tell apart — which is why
    /// this is stored while "a newer Run exists" stays derived (#94's design D2, extended).
    /// </summary>
    public DateTimeOffset? DismissedAt { get; private set; }

    /// <summary>Set while AwaitingInput; the resume check reads comments from this moment on.</summary>
    public DateTimeOffset? WaitingSince { get; private set; }

    /// <summary>BR-011/DEC-038: all three null together means "unknown" — never invented.</summary>
    public long? UsageInputTokens { get; private set; }

    public long? UsageOutputTokens { get; private set; }

    public decimal? CostUsd { get; private set; }

    public void MarkExecuting(DateTimeOffset at)
    {
        State = RunState.Executing;
        StartedAt ??= at;
    }

    /// <summary>Phase 1 is its own state so the cap and the UI can tell proposing from doing.</summary>
    public void MarkPlanning(DateTimeOffset at)
    {
        State = RunState.Planning;
        StartedAt ??= at;
    }

    public void Succeed(
        DateTimeOffset at,
        string? outputLink,
        long? inputTokens,
        long? outputTokens,
        decimal? costUsd
    )
    {
        State = RunState.Succeeded;
        EndedAt = at;
        OutputLink = outputLink;
        UsageInputTokens = inputTokens;
        UsageOutputTokens = outputTokens;
        CostUsd = costUsd;
    }

    /// <summary>Phase 1 done: the proposal is recorded and the Run waits, untimed (BR-006).</summary>
    public void AwaitApproval(DateTimeOffset at, string plan)
    {
        State = RunState.AwaitingApproval;
        Plan = plan;
        EndedAt = null;
        UpdatedNow(at);
    }

    /// <summary>Approved: back to Queued for phase 2, with the stamp that routes it there.</summary>
    public void Approve(DateTimeOffset at)
    {
        State = RunState.Queued;
        ApprovedAt = at;
    }

    /// <summary>
    /// The pass ended by asking. Shaped exactly like <see cref="AwaitApproval"/> — the container
    /// exits, the wait is untimed (BR-006 grown from approval to human waits), and the Story
    /// stays blocked (BR-001). <paramref name="at"/> is also the resume check's watermark: only
    /// comments after it can wake this Run.
    /// </summary>
    public void AwaitInput(DateTimeOffset at)
    {
        State = RunState.AwaitingInput;
        WaitingSince = at;
        EndedAt = null;
        UpdatedNow(at);
    }

    /// <summary>An answer arrived: back to Queued, and ordinary dispatch does the rest.</summary>
    public void Resume()
    {
        State = RunState.Queued;
        WaitingSince = null;
    }

    /// <summary>Rejected — terminal (BR-012), so the Story is free again (BR-001).</summary>
    public void Reject(DateTimeOffset at) => Cancel(at);

    /// <summary>
    /// Cancelled by a human. Terminal immediately (design D1): the Story frees now, not when
    /// some worker acknowledges. No failure reason — a deliberate act is not a fault.
    /// </summary>
    public void Cancel(DateTimeOffset at)
    {
        State = RunState.Cancelled;
        EndedAt = at;
    }

    /// <summary>
    /// Whether a human's cancellation has already decided this Run's fate. The executor asks
    /// before spending and before publishing, and its own outcome must never overwrite it
    /// (design D3) — the human always wins that race.
    /// </summary>
    public bool IsCancelled => State == RunState.Cancelled;

    /// <summary>Phase 1 leaves StartedAt as the first phase's start; the wait is not work.</summary>
    void UpdatedNow(DateTimeOffset at) => StartedAt ??= at;

    /// <summary>
    /// Records that somebody looked and chose not to re-run. Idempotent, and deliberately does not
    /// touch the state: a dismissal says what a person decided, never what happened (BR-014).
    /// </summary>
    public bool Dismiss(DateTimeOffset at)
    {
        if (State != RunState.Failed)
        {
            return false;
        }

        DismissedAt ??= at;
        return true;
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

    /// <summary>A rejected Plan, or a cancelled Run (#23). Terminal, and outside BR-001's filter.</summary>
    Cancelled = 7,

    /// <summary>
    /// The agent asked and a human has not yet answered (#78). Active for BR-001 — a Story
    /// mid-conversation must not start a second Run — and untimed like approval (BR-006).
    /// </summary>
    AwaitingInput = 8,
}

/// <summary>
/// BR-001's notion of "active", defined exactly once. Twice now a hand-written copy of this
/// list in the creation pre-check has drifted from the partial index it fronts — first when
/// terminal states arrived, then again when <see cref="RunState.Cancelled"/> did — and each
/// time the database was right while the pre-check lied. The index filter is generated from
/// this array, so the two cannot disagree again.
/// </summary>
static class RunStates
{
    public static readonly RunState[] Active =
    [
        RunState.Queued,
        RunState.Planning,
        RunState.AwaitingApproval,
        RunState.Executing,
        RunState.AwaitingInput,
    ];

    /// <summary>
    /// Terminal is the complement, never a second list (#144). The comment above this class exists
    /// because a hand-written copy drifted twice; a copy of the *inverse* would drift the same way.
    /// </summary>
    public static bool IsTerminal(RunState state) => !Active.Contains(state);

    /// <summary>The SQL the partial unique index filters on — same list, no second copy.</summary>
    public static string ActiveStateFilter() =>
        $"\"State\" IN ({string.Join(", ", Active.Select(state => $"'{state}'"))})";
}
