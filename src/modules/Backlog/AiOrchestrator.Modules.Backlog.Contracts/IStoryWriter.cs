namespace AiOrchestrator.Modules.Backlog.Contracts;

/// <summary>
/// The write surface other modules use to act on a Story — the Agent's actions (UC-017/018/019)
/// reach the vendor through here, so the Runs module never touches the Backlog implementation.
/// <para>
/// Each returns a failure reason rather than throwing: an action that could not be carried out
/// ends its Run with that sentence, and a vendor refusal is information, not an exception.
/// </para>
/// </summary>
public interface IStoryWriter
{
    /// <summary>Null on success; otherwise why it could not be done.</summary>
    Task<string?> AddComment(
        Guid projectId,
        string vendorStoryId,
        string comment,
        CancellationToken cancellationToken = default
    );

    /// <summary>Applies one label — the grill's ready label rides UC-008's write path.</summary>
    Task<string?> ApplyLabel(
        Guid projectId,
        string vendorStoryId,
        string label,
        CancellationToken cancellationToken = default
    );

    Task<string?> SetState(
        Guid projectId,
        string vendorStoryId,
        string state,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Replaces any existing <c>estimate:*</c> label with this one, so a Story carries exactly
    /// one estimate rather than a history of them.
    /// </summary>
    Task<string?> SetEstimate(
        Guid projectId,
        string vendorStoryId,
        int estimate,
        CancellationToken cancellationToken = default
    );
}
