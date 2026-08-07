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
        AgentRuntime? runtime,
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
    /// Which prompt this Automation runs, resolved against the project's prompts directory (#150).
    /// Null means the convention.
    /// <para>
    /// Called <c>RubricPath</c> until #162, when the grill it was named for stopped existing. The
    /// column was never only the grill's after #150 made it how a repository prompt names its file —
    /// which is why this change renames it rather than removing it with the action.
    /// </para>
    /// </summary>
    public string? PromptPath { get; private set; }

    /// <summary>
    /// What this Automation applies to the Story when a Run of it succeeds (#165): the workflow's
    /// outgoing edges, and any mark that goes with them. Empty means it ends silently.
    /// <para>
    /// A set rather than one label, because one hand-off was one edge and nothing else — no way to
    /// also mark the Story, and no way to wire a second listener. Held in declaration order for
    /// display; order is not a priority, because the labels come back as vendor deliveries and are
    /// matched then (design D3).
    /// </para>
    /// <para>
    /// For a grill, an empty set still means the documented default rather than silence — that
    /// default lives in the executor, not here (grill design D5).
    /// </para>
    /// </summary>
    public IReadOnlyList<string> OutputLabels
    {
        get => _outputLabels;
        private set => _outputLabels = Distinct(value);
    }

    // EF materialises into the backing field, so the property's dedupe cannot be relied on for
    // reads out of the database — which is correct: what was stored was already deduped, and
    // re-normalising on load would quietly rewrite history.
    List<string> _outputLabels = [];

    public AutomationAction Action { get; private set; }

    /// <summary>
    /// Null means the Project default, resolved at execution time (project-runtimes) — changing
    /// the default changes future Runs without touching this row.
    /// </summary>
    public AgentRuntime? Runtime { get; private set; }

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
        AgentRuntime? runtime,
        bool requiresApproval,
        TimeSpan timeout,
        string? promptPath = null,
        IReadOnlyList<string>? outputLabels = null
    ) =>
        new(projectId, triggerLabel, triggerState, action, runtime, requiresApproval, timeout)
        {
            PromptPath = promptPath,
            OutputLabels = outputLabels ?? [],
        };

    /// <summary>Applies an edit. The overlap gate runs after this, against the new shape.</summary>
    public void UpdateTo(
        string triggerLabel,
        string? triggerState,
        AutomationAction action,
        AgentRuntime? runtime,
        bool requiresApproval,
        TimeSpan timeout,
        string? promptPath = null,
        IReadOnlyList<string>? outputLabels = null
    )
    {
        TriggerLabel = triggerLabel;
        TriggerState = triggerState;
        Action = action;
        Runtime = runtime;
        RequiresApproval = requiresApproval;
        Timeout = timeout;
        PromptPath = promptPath;
        OutputLabels = outputLabels ?? [];
    }

    /// <summary>
    /// One label twice is one label. The vendor compares case-insensitively (DEC-056), so a set that
    /// held both spellings would apply the same label twice and draw two edges to one node. The first
    /// spelling wins, because it is the one the Admin typed.
    /// </summary>
    static List<string> Distinct(IReadOnlyList<string> labels)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return [.. labels.Where(label => seen.Add(label))];
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

/// <summary>
/// The catalogue, after #162 collapsed it to one: an Automation runs the repository's own prompt.
/// <para>
/// Kept as an enum with one member rather than removed, because the field is where the second thing
/// will be said — grants are the named follow-up, and "which prompt, under which grants" belongs
/// beside "which trigger". A stored row saying <c>RepositoryPrompt</c> also explains itself; a row
/// with the column gone relies on the reader knowing what year it was written.
/// </para>
/// <para>
/// What left with the other seven: the orchestrator opening a pull request, writing a comment,
/// transitioning a state or parsing an estimate on the agent's behalf. The prompt does those, holding
/// the same credential, or they do not happen (DEC-062).
/// </para>
/// </summary>
enum AutomationAction
{
    /// <summary>
    /// Runs a prompt the project itself wrote, named by <see cref="Automation.PromptPath"/> and
    /// resolved against the project's prompts directory (#150). The body is the prompt; any
    /// frontmatter is another runner's wiring and is ignored, because the Automation is already this
    /// product's.
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
