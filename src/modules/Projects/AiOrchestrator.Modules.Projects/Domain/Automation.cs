using AiOrchestrator.BuildingBlocks.Domain;

namespace AiOrchestrator.Modules.Projects.Domain;

/// <summary>
/// "A Story labelled X makes an Agent do Y." The product's central noun, owned by BC-001
/// Project Configuration along with its validation.
/// </summary>
sealed class Automation : Aggregate
{
    Automation() { }

    Automation(
        Guid projectId,
        string triggerLabel,
        string? triggerState,
        AutomationAction action,
        AgentRuntime runtime,
        bool requiresApproval,
        TimeSpan timeout
    )
    {
        ProjectId = projectId;
        TriggerLabel = triggerLabel;
        TriggerState = triggerState;
        Action = action;
        Runtime = runtime;
        RequiresApproval = requiresApproval;
        Timeout = timeout;
        Enabled = true;
    }

    public Guid ProjectId { get; private set; }

    public string TriggerLabel { get; private set; } = string.Empty;

    /// <summary>
    /// The vendor's own state string, or null for "any state". Not normalised, by decision: the
    /// Mirror keeps vendor vocabulary (DEC-045), so a trigger names what the board actually says.
    /// </summary>
    public string? TriggerState { get; private set; }

    /// <summary>
    /// Grill only: where the readiness document lives in the connected repository. Null means
    /// the framework's convention — the default is code, not data (grill design D5).
    /// </summary>
    public string? RubricPath { get; private set; }

    /// <summary>Grill only: the label applied when the bar is met. Null means the convention.</summary>
    public string? OutputLabel { get; private set; }

    public AutomationAction Action { get; private set; }

    public AgentRuntime Runtime { get; private set; }

    /// <summary>Per-Automation, not global (DEC-039): true routes the Run through a Plan (BR-007).</summary>
    public bool RequiresApproval { get; private set; }

    /// <summary>Per Agent phase (BR-005). Default 30 minutes, set by the caller.</summary>
    public TimeSpan Timeout { get; private set; }

    /// <summary>
    /// Present from the start even though #15 owns toggling it: BR-003 only considers *enabled*
    /// Automations, so the overlap rule cannot be written without it.
    /// </summary>
    public bool Enabled { get; private set; }

    public static Automation Create(
        Guid projectId,
        string triggerLabel,
        string? triggerState,
        AutomationAction action,
        AgentRuntime runtime,
        bool requiresApproval,
        TimeSpan timeout,
        string? rubricPath = null,
        string? outputLabel = null
    ) =>
        new(projectId, triggerLabel, triggerState, action, runtime, requiresApproval, timeout)
        {
            RubricPath = rubricPath,
            OutputLabel = outputLabel,
        };

    /// <summary>Applies an edit. The overlap gate runs after this, against the new shape.</summary>
    public void UpdateTo(
        string triggerLabel,
        string? triggerState,
        AutomationAction action,
        AgentRuntime runtime,
        bool requiresApproval,
        TimeSpan timeout,
        string? rubricPath = null,
        string? outputLabel = null
    )
    {
        TriggerLabel = triggerLabel;
        TriggerState = triggerState;
        Action = action;
        Runtime = runtime;
        RequiresApproval = requiresApproval;
        Timeout = timeout;
        RubricPath = rubricPath;
        OutputLabel = outputLabel;
    }

    /// <summary>
    /// Disabling makes the Automation invisible to BR-003 and to matching; enabling makes it
    /// visible again, which is why only enabling re-runs the overlap check.
    /// </summary>
    public void SetEnabled(bool enabled) => Enabled = enabled;

    /// <summary>
    /// BR-003's <i>intersects</i>, made precise (design D1): two Automations overlap when some
    /// Story could match both.
    /// <para>
    /// The case that matters is subsumption — a trigger with no state constraint matches every
    /// Story carrying the label, including the ones a state-specific trigger matches. Treating
    /// that pair as compatible would let one event match two Automations, which is exactly the
    /// non-determinism BR-003 exists to prevent.
    /// </para>
    /// <para>
    /// Deliberately symmetric: whichever is saved second is refused. That asymmetry in *outcome*
    /// is the price of validating at write time (DEC-033) rather than resolving priority at read
    /// time, and it is the behaviour an Admin will actually notice.
    /// </para>
    /// </summary>
    /// <summary>
    /// The same trigger, whatever either one's <see cref="Enabled"/> says (#147, design D3).
    /// <para>
    /// Distinct from <see cref="Overlaps"/> on purpose. Subsumption is about *matching*, and a
    /// disabled Automation matches nothing, so it correctly ignores them. But two rows carrying one
    /// trigger are one trigger either way, and allowing them means the conflict is discovered at
    /// enable time by somebody who did not create it.
    /// </para>
    /// </summary>
    public bool IsSameTriggerAs(Automation other) =>
        SameLabel(TriggerLabel, other.TriggerLabel)
        && (
            (TriggerState is null && other.TriggerState is null)
            || (
                TriggerState is not null
                && other.TriggerState is not null
                && SameLabel(TriggerState, other.TriggerState)
            )
        );

    /// <summary>
    /// One comparison for labels and states, used by the guard and by matching (#147, design D4).
    /// Two callers, one rule: the previous arrangement let the guard accept a differently-cased
    /// trigger that the matcher would then never fire, which failed silently.
    /// </summary>
    public static bool SameLabel(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    public bool Overlaps(Automation other)
    {
        // Disabled Automations are invisible to the rule — BR-003 says "existing enabled".
        if (!Enabled || !other.Enabled)
        {
            return false;
        }

        // Labels are the vendor's; compare them the way the vendor does. That sentence was here
        // before #147 with an Ordinal comparison under it — GitHub's label names are
        // case-insensitive, so the comment was right and the code was not.
        if (!SameLabel(TriggerLabel, other.TriggerLabel))
        {
            return false;
        }

        // Either side unconstrained by state subsumes the other.
        if (TriggerState is null || other.TriggerState is null)
        {
            return true;
        }

        return SameLabel(TriggerState, other.TriggerState);
    }
}

/// <summary>The MVP action catalogue (DEC-026). All four configurable; only Implement→PR executes yet.</summary>
enum AutomationAction
{
    ImplementToPullRequest = 1,
    RefineOrComment = 2,
    TransitionState = 3,
    Estimate = 4,

    /// <summary>
    /// Interrogates a Story to its project's readiness bar through the conversational wait
    /// (#79, revising DEC-026's four-action catalogue as DEC-048).
    /// </summary>
    GrillToReady = 5,

    /// <summary>
    /// Turns a ready Story into a documentation pull request — the reviewable step between
    /// ready and implemented (#80, UC-025; DEC-048 licenses the growth).
    /// </summary>
    ProposeSpec = 6,

    /// <summary>
    /// Closes the Story's open change by following the connected repository's own close-out
    /// procedure (#123, DEC-048). The product knows that a change closes, never how — that
    /// document belongs to the project, exactly as the grill's readiness bar does.
    /// </summary>
    SyncChange = 7,

    /// <summary>
    /// Runs a prompt the project itself wrote, named by <c>RubricPath</c> and resolved against the
    /// project's prompts directory (#150, DEC-048's lane again). The body is the prompt; any
    /// frontmatter is another runner's wiring and is ignored, because the Automation is already this
    /// product's. The answer becomes one Story comment and nothing else — a prompt cannot widen its
    /// own surface by asking to.
    /// </summary>
    RepositoryPrompt = 8,
}

/// <summary>
/// Runtimes an Automation can name (DEC-012), in the order they were added. opencode's contract
/// was observed rather than guessed before it was added here (DEC-044, closing OPN-004).
/// </summary>
enum AgentRuntime
{
    ClaudeCodeHeadless = 1,
    OpenCode = 2,
}
