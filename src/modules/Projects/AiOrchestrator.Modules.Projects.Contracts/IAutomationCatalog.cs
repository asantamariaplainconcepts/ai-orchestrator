namespace AiOrchestrator.Modules.Projects.Contracts;

/// <summary>
/// The read surface other modules match against — the second Contracts assembly, and the one
/// design D6 of module-integration-events promised. The Projects module registers the
/// implementation; consumers never see it.
/// </summary>
public interface IAutomationCatalog
{
    /// <summary>The enabled Automations of a Project, as triggers to match against.</summary>
    Task<IReadOnlyList<AutomationTrigger>> EnabledAutomations(
        Guid projectId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// What execution needs about one Automation — action, runtime and timeout as the worker
    /// consumes them. Deliberately <b>not</b> filtered by <c>Enabled</c>: disabling stops
    /// future matches (UC-006), it does not kill work already in flight, and a Run that
    /// started under an Automation must be able to finish under it. Null only when the
    /// Automation does not exist in this project.
    /// </summary>
    Task<AutomationDetail?> Detail(
        Guid projectId,
        Guid automationId,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Enum <b>names</b>, not ordinals — the self-describing convention everywhere else.</summary>
public sealed record AutomationDetail(
    Guid AutomationId,
    string TriggerLabel,
    string Action,
    /// <summary>Null means the Project default, resolved at execution time (project-runtimes).</summary>
    string? Runtime,
    TimeSpan Timeout,
    /// <summary>Grill only; null means the framework's convention (grill design D5).</summary>
    string? PromptPath = null,
    /// <summary>
    /// The <b>marks</b> applied to the Story when a Run succeeds (#165). Empty means it marks
    /// nothing. Since #310 these carry no meaning about the flow: the lifecycle move is
    /// <see cref="ToStage"/>, and a mark matching no stage is an ordinary mark rather than a
    /// dangling edge.
    /// </summary>
    IReadOnlyList<string>? OutputLabels = null,
    /// <summary>
    /// The sandbox port to publish while a Run of this Automation executes (run-previews). Null
    /// means no preview, which is every Automation until an Admin names one.
    /// </summary>
    int? PreviewPort = null,
    /// <summary>
    /// The model this Automation's Runs think with (#291). Null means the deployment's, resolved
    /// at execution time — every Automation that existed before this, and every one whose Admin
    /// has not chosen.
    /// </summary>
    string? Model = null,
    /// <summary>
    /// The to-stage of the one transition this Automation claims (#310, design D9): the lifecycle
    /// move its Run applies on success, alongside every mark, through the same licensed write. Null
    /// means it claims no transition — it acts, it may mark the Story, and the flow ends there.
    /// <para>
    /// This is the only thing the Runs module learns about the lifecycle, and deliberately so: the
    /// hand-off still travels through the vendor label and comes back as an ordinary StoryChanged,
    /// so nothing here knows what happens next and no dispatch machinery is added.
    /// </para>
    /// </summary>
    string? ToStage = null
);

/// <summary>
/// What matching needs and nothing more: the trigger and the id to record on the Run. Action,
/// runtime and timeout stay inside Projects until the dispatch worker needs them — through this
/// same surface, not through the event.
/// <para>
/// The lane flag is gone with DEC-067: every Run is single-phase, so there is no lane to choose.
/// What stops work is the hold on the Story, which matching never sees — the refusal lives at
/// creation, where <i>Run now</i> passes too (BR-007, BR-013).
/// </para>
/// </summary>
public sealed record AutomationTrigger(
    Guid AutomationId,
    string TriggerLabel,
    string? TriggerState
);
