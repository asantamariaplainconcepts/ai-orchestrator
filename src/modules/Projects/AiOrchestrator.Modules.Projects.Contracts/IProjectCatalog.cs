namespace AiOrchestrator.Modules.Projects.Contracts;

/// <summary>
/// What other modules may ask about a Project's standing. Today that is one question, and it has
/// two callers: Backlog decides whether to poll, Runs decides whether to start work.
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
}
