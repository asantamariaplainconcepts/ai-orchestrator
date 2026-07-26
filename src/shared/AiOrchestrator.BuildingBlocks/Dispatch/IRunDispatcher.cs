namespace AiOrchestrator.BuildingBlocks.Dispatch;

/// <summary>
/// Hands a Run to the execution substrate. Product vocabulary only — no queue type appears here,
/// so a module can dispatch without referencing a cloud SDK (the same rule that keeps
/// <see cref="Secrets.ISecretResolver"/> abstract).
/// <para>
/// The Run's id is the whole message (design D2): the worker reads the Run, its Story and its
/// Automation from the database. One source of truth, nothing to go stale between enqueue and
/// execution, and a message that does not grow every time the Run model gains a field.
/// </para>
/// </summary>
public interface IRunDispatcher
{
    /// <summary>
    /// Enqueues the Run for execution. Returns once the substrate has accepted it — acceptance
    /// means "will be delivered", never "has run".
    /// </summary>
    Task Dispatch(Guid runId, CancellationToken cancellationToken = default);
}
