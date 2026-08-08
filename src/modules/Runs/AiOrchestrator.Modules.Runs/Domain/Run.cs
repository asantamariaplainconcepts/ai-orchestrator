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

    Run(Guid projectId, RunLocus locus, DateTimeOffset createdAt)
    {
        ProjectId = projectId;
        Locus = locus;
        State = RunState.Queued;
        CreatedAt = createdAt;
    }

    public Guid ProjectId { get; private set; }

    /// <summary>Null exactly when this Run targets a change instead (run-on-a-pr).</summary>
    public string? VendorStoryId { get; private set; }

    /// <summary>Null exactly when this Run targets a change: ad-hoc text has no Automation.</summary>
    public Guid? AutomationId { get; private set; }

    /// <summary>
    /// The open change this Run updates, or null for a story Run. A Run targets exactly one of a
    /// Story or a change — never both, never neither — and the two Create shapes are the whole of
    /// that invariant's enforcement.
    /// </summary>
    public int? TargetChangeNumber { get; private set; }

    public string? TargetChangeUrl { get; private set; }

    /// <summary>The change's title at launch, for the Agent's framing and the surfaces' label.</summary>
    public string? TargetChangeTitle { get; private set; }

    /// <summary>The change's head branch at launch — what the workspace checks out by name.</summary>
    public string? TargetChangeBranch { get; private set; }

    /// <summary>
    /// The Member's ad-hoc instruction, recorded on the Run (a record, never configuration): the
    /// prompt body a change Run executes, readable in its detail afterwards.
    /// </summary>
    public string? Instruction { get; private set; }

    /// <summary>
    /// The runtime the launch named, or null for the default — a change Run has no Automation to
    /// carry one (design D4).
    /// </summary>
    public string? RuntimeName { get; private set; }

    /// <summary>
    /// The model the launch named, for this Run only. Null means the resolution chain decides —
    /// the Automation's, then the deployment's. Recorded so a later Run of the same Automation
    /// resolves as if this choice had never happened.
    /// </summary>
    public string? Model { get; private set; }

    /// <summary>
    /// The model this Run actually executed on, written when execution resolves it. Distinct from
    /// <see cref="Model"/>, which records only what a human asked for: a Run that inherited its
    /// model has nothing in the first and something real in the second, and a cost figure is
    /// uninterpretable without knowing which model produced it (BR-011, design D7).
    /// <para>
    /// Null where the runtime launched with no model at all — the state every Run was in before
    /// this existed, and still the honest answer for a deployment that chose nothing.
    /// </para>
    /// </summary>
    public string? ResolvedModel { get; private set; }

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
        RunLocus locus,
        DateTimeOffset createdAt,
        string? runtimeName = null,
        string? model = null
    ) =>
        new(projectId, locus, createdAt)
        {
            VendorStoryId = vendorStoryId,
            AutomationId = automationId,
            RuntimeName = runtimeName,
            Model = Normalised(model),
        };

    /// <summary>The change-targeted shape (run-on-a-pr): no Story, no Automation, one change.</summary>
    public static Run CreateForChange(
        Guid projectId,
        int changeNumber,
        string changeUrl,
        string changeTitle,
        string changeBranch,
        string instruction,
        string? runtimeName,
        RunLocus locus,
        DateTimeOffset createdAt,
        string? model = null
    ) =>
        new(projectId, locus, createdAt)
        {
            Model = Normalised(model),
            TargetChangeNumber = changeNumber,
            TargetChangeUrl = changeUrl,
            TargetChangeTitle = changeTitle,
            TargetChangeBranch = changeBranch,
            Instruction = instruction,
            RuntimeName = runtimeName,
        };

    public void MarkDispatched(DateTimeOffset at) => DispatchedAt = at;

    /// <summary>
    /// Where this Run executes (#210) — fixed at creation, because the workspace it needs and
    /// the preconditions it was checked against (BR-016) are decided there, not claimed later.
    /// </summary>
    public RunLocus Locus { get; private set; }

    /// <summary>The host folder a Local run worked in (BR-014's audit, extended). Null for Pod.</summary>
    public string? WorkingFolder { get; private set; }

    /// <summary>The branch a Local run left behind — its output, where Pod runs carry a PR link.</summary>
    public string? BranchName { get; private set; }

    /// <summary>Stamped at execution, when the workspace actually existed — never predicted.</summary>
    public void RecordLocalExecution(string workingFolder, string branchName)
    {
        WorkingFolder = workingFolder;
        BranchName = branchName;
    }

    /// <summary>When execution actually began — the claim, not the enqueue (BR-014).</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>Why a Run failed, in one honest sentence. Null on success.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Where the work landed — the PR URL for ImplementToPullRequest (BR-014).</summary>
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

    /// <summary>
    /// What execution resolved the model to, written where the resolution happens so the Run's
    /// own record can answer "what did this cost me, on what" without re-deriving a chain whose
    /// inputs may since have changed.
    /// </summary>
    public void RecordResolvedModel(string? model) => ResolvedModel = Normalised(model);

    /// <summary>Whitespace is absence — the same rule the Automation applies, for the same reason.</summary>
    static string? Normalised(string? model) =>
        string.IsNullOrWhiteSpace(model) ? null : model.Trim();

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
/// Where a Run executes (#210). Not a routing fact — the queue and the worker are identical for
/// both — but a workspace fact: <see cref="Pod"/> clones fresh, <see cref="Local"/> works in the
/// Connector's configured folder on the host (self-host flavour, DEC-049).
/// </summary>
enum RunLocus
{
    Pod = 1,
    Local = 2,
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
