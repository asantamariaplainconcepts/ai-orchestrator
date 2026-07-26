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
    /// What execution needs about one enabled Automation — action, runtime and timeout as the
    /// worker consumes them. Null when disabled, deleted, or another project's.
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
    string Runtime,
    bool RequiresApproval,
    TimeSpan Timeout
);

/// <summary>
/// What matching needs and nothing more: the trigger, the lane flag, and the id to record on
/// the Run. Action, runtime and timeout stay inside Projects until the dispatch worker needs
/// them — through this same surface, not through the event.
/// </summary>
public sealed record AutomationTrigger(
    Guid AutomationId,
    string TriggerLabel,
    string? TriggerState,
    bool RequiresApproval
);
