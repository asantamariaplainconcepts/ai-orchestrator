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
}
