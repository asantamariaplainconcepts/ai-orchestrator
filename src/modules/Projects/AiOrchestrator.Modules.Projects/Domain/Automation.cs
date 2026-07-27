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
    public string? ReadyLabel { get; private set; }

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
        string? readyLabel = null
    ) =>
        new(projectId, triggerLabel, triggerState, action, runtime, requiresApproval, timeout)
        {
            RubricPath = rubricPath,
            ReadyLabel = readyLabel,
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
        string? readyLabel = null
    )
    {
        TriggerLabel = triggerLabel;
        TriggerState = triggerState;
        Action = action;
        Runtime = runtime;
        RequiresApproval = requiresApproval;
        Timeout = timeout;
        RubricPath = rubricPath;
        ReadyLabel = readyLabel;
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
    public bool Overlaps(Automation other)
    {
        // Disabled Automations are invisible to the rule — BR-003 says "existing enabled".
        if (!Enabled || !other.Enabled)
        {
            return false;
        }

        // Labels are the vendor's; compare them the way the vendor does.
        if (!string.Equals(TriggerLabel, other.TriggerLabel, StringComparison.Ordinal))
        {
            return false;
        }

        // Either side unconstrained by state subsumes the other.
        if (TriggerState is null || other.TriggerState is null)
        {
            return true;
        }

        return string.Equals(TriggerState, other.TriggerState, StringComparison.Ordinal);
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
