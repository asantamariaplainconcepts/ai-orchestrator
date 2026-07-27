namespace AiOrchestrator.Modules.Backlog.Contracts;

/// <summary>
/// Repository-level label writes, as other modules may reach them. Distinct from
/// <see cref="IStoryWriter"/> on purpose: everything there names a Story, and this names none —
/// it is about what a Member can <i>choose</i> in the vendor's interface, before any Story has
/// been labelled at all.
/// </summary>
public interface ILabelWriter
{
    /// <summary>
    /// Ensures every named label exists in the project's connected repository.
    /// <para>
    /// Returns null when they all do. Otherwise a sentence saying why not — including the
    /// ordinary case of a project with no Connector, and the vendor that has no
    /// repository-level labels at all. A caller reports that sentence rather than failing:
    /// labels are what makes an Automation reachable, not what makes it valid.
    /// </para>
    /// </summary>
    Task<string?> EnsureLabels(
        Guid projectId,
        IReadOnlyList<string> labels,
        CancellationToken cancellationToken = default
    );
}
