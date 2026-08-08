namespace AiOrchestrator.BuildingBlocks.Dispatch;

/// <summary>
/// What happens to a Run the outbox consumer claimed (#246): executed in this process, or in a
/// sandbox of its own. The seam exists so the consumer stays one class while the habitat decides the
/// arrangement in composition — the same shape the dispatcher seam already has, one level down.
/// <para>
/// Product vocabulary only: no docker type appears here, so the consumer can be tested against a
/// fake and a fourth arrangement is a registration, never a subscriber edit.
/// </para>
/// </summary>
public interface IDispatchedRunHandler
{
    /// <summary>
    /// Takes the Run to a terminal state, however this habitat executes. Returning means the
    /// execution happened (the Run's own state carries success or failure); throwing means it
    /// could not happen at all — and nothing retries it (BR-004).
    /// </summary>
    Task Handle(Guid runId, CancellationToken cancellationToken);
}
