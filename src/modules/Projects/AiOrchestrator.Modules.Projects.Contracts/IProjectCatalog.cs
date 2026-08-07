namespace AiOrchestrator.Modules.Projects.Contracts;

/// <summary>
/// What other modules may ask about a Project's standing. Two questions now: Backlog decides
/// whether to poll and Runs decides whether to start work with <see cref="AcceptsWork"/>, and
/// the inbox names each entry's Project with <see cref="Name"/> — the list is cross-project, so
/// a row without its Project's name answers "which #491?" with silence.
/// <para>
/// Asked per decision and never cached (#121, design D1), for the reason
/// <c>ISecretResolver</c> resolves per read: a Project is archived while the application runs,
/// and a poller holding a snapshot would keep polling something an Admin just retired — a
/// failure that reads as "archiving does not work" rather than "the process is stale".
/// </para>
/// </summary>
public interface IProjectCatalog
{
    /// <summary>
    /// True when the Project accepts new work. False when it is archived, and false when it does
    /// not exist — a caller acting on an unknown Project should stop for the same reason.
    /// </summary>
    Task<bool> AcceptsWork(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>The Project's display name, or null when it does not exist.</summary>
    Task<string?> Name(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every active Project's id (inbox-open-prs). Exists for cross-project surfaces whose caller
    /// may see everything: <c>IProjectPermissions.VisibleProjects</c> answers null for the owner
    /// and the self-host habitat precisely so "all" costs no query — which leaves such a surface
    /// with no list to iterate, and a per-project vendor read needs one. Archived projects are
    /// excluded: their repositories are not anyone's review queue.
    /// </summary>
    Task<IReadOnlyList<Guid>> ActiveProjectIds(CancellationToken cancellationToken = default);
}
