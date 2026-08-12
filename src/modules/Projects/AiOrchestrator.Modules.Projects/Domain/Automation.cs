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
        TimeSpan timeout
    )
    {
        ProjectId = projectId;
        TriggerLabel = triggerLabel;
        TriggerState = triggerState;
        Action = action;
        Runtime = runtime;
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
    /// The <b>to-stage</b> of the one transition this Automation claims of its project's lifecycle:
    /// the label its Run applies as the lifecycle move when it succeeds (#310, design D2). The
    /// transition's <b>from</b>-stage is <see cref="TriggerLabel"/> — that is already what makes
    /// this Automation fire, and naming it twice would be a second description of one fact.
    /// <para>
    /// <b>Nullable, and null is an answer</b> (design D3): "claims no transition — it acts, it may
    /// mark the Story, and the flow ends there." AC 13's "exactly one" was read at spec review as
    /// <i>at most one, never two</i>, because two cases have to stay expressible: DEC-053's
    /// standalone Automation, which acts on its own when somebody applies its label, and the last
    /// stage of a lifecycle, which has no outgoing boundary at all.
    /// </para>
    /// <para>
    /// Single-valued rather than a set, which is what makes branching <i>unrepresentable</i> rather
    /// than merely discouraged: there is no field in which a second transition could be written.
    /// </para>
    /// <para>
    /// Not derived from adjacency in the stored stage list, though that would store strictly less:
    /// reordering the list would then silently rewrite what every neighbouring Automation hands on,
    /// which is ADR-0019's invisible-at-the-call-site failure in a new field. AC 5 requires that
    /// moving one Automation change no other's claimed transition.
    /// </para>
    /// </summary>
    public string? ToStage { get; private set; }

    /// <summary>
    /// The <b>marks</b> this Automation applies to the Story when a Run of it succeeds (#165, split
    /// out of the edge by #310): labels carrying no meaning about the flow. Empty means it marks
    /// nothing.
    /// <para>
    /// This field used to do double duty — "the workflow's outgoing edges, <i>and</i> any mark that
    /// goes with them" — and separating those two is the substance of #310. The edge is now
    /// <see cref="ToStage"/>. A mark that matches no stage and no other Automation's trigger is an
    /// ordinary mark, not a dangling edge and not an incomplete configuration; after the separation
    /// there is nothing for such a warning to be about.
    /// </para>
    /// <para>
    /// A set rather than one label, because one hand-off was one edge and nothing else — no way to
    /// also mark the Story. Held in declaration order for display; order is not a priority, because
    /// the labels come back as vendor deliveries and are matched then (design D3).
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

    /// <summary>
    /// The model this Automation's Runs think with. Null — the default, and every Automation that
    /// existed before this — means the deployment's, resolved at execution time, so changing the
    /// default changes future Runs without touching this row.
    /// <para>
    /// Deliberately a free string rather than an enum: the models a runtime offers are a property
    /// of that runtime's CLI and of the session it holds, not of this product's vocabulary. An
    /// enum here would have to be edited every time a provider ships anything, and #291's own
    /// measurement showed two of three plausible hardcoded aliases already broken.
    /// </para>
    /// <para>
    /// Resolved independently of <see cref="Runtime"/>, which means the two can disagree. That is
    /// accepted (design D4): the runtime refuses and the Run says which model and which runtime,
    /// because coupling them would let a runtime change silently rewrite the Admin's stated
    /// intent.
    /// </para>
    /// </summary>
    public string? Model { get; private set; }

    /// <summary>Per Agent phase (BR-005). Default 30 minutes, set by the caller.</summary>
    public TimeSpan Timeout { get; private set; }

    /// <summary>
    /// The port inside the Run's sandbox to publish while it executes, so a Member can look at
    /// the change running instead of at a description of it. Null — the default and every
    /// existing Automation — means no preview.
    /// <para>
    /// On the Automation rather than the Project (run-previews design D3, closing its open
    /// question): the Project knows the application, but two Automations over one repository may
    /// start different things, and only the prompt knows whether its change is runnable at all.
    /// </para>
    /// <para>
    /// Naming the port is the whole contract; nothing detects a listening one. Until something
    /// inside serves, the preview reads as "nothing serving yet" — a state of a live Run, never
    /// an error (ADR-0010: asked, never inferred).
    /// </para>
    /// </summary>
    public int? PreviewPort { get; private set; }

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
        TimeSpan timeout,
        string? promptPath = null,
        IReadOnlyList<string>? outputLabels = null,
        int? previewPort = null,
        string? model = null,
        string? toStage = null
    ) =>
        new(projectId, triggerLabel, triggerState, action, runtime, timeout)
        {
            PromptPath = promptPath,
            OutputLabels = outputLabels ?? [],
            PreviewPort = previewPort,
            Model = Normalised(model),
            ToStage = Normalised(toStage),
        };

    /// <summary>Applies an edit. The overlap gate runs after this, against the new shape.</summary>
    public void UpdateTo(
        string triggerLabel,
        string? triggerState,
        AutomationAction action,
        AgentRuntime? runtime,
        TimeSpan timeout,
        string? promptPath = null,
        IReadOnlyList<string>? outputLabels = null,
        int? previewPort = null,
        string? model = null,
        string? toStage = null
    )
    {
        TriggerLabel = triggerLabel;
        TriggerState = triggerState;
        Action = action;
        Runtime = runtime;
        Timeout = timeout;
        PromptPath = promptPath;
        OutputLabels = outputLabels ?? [];
        PreviewPort = previewPort;
        Model = Normalised(model);
        ToStage = Normalised(toStage);
    }

    /// <summary>
    /// Whitespace is absence. A form that sends an empty field means "inherit", and storing "" as
    /// though it were a model would resolve to a model named nothing at execution time. The same
    /// reading applies to the claimed to-stage: a blank field is "claims no transition", never a
    /// stage whose name is nothing.
    /// </summary>
    static string? Normalised(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
