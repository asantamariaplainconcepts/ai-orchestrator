namespace AiOrchestrator.Modules.Backlog.Contracts;

/// <summary>
/// A document from the connected repository's default branch, as other modules may read it —
/// the grill action reads its readiness rubric here. Live, never mirrored (BR-008).
/// </summary>
public interface IDocumentReader
{
    /// <summary>
    /// <see cref="DocumentResult.Failure"/> is null on success; a missing document is a failure
    /// naming the path, because the caller's contract is fail-before-write (grill design D2).
    /// </summary>
    Task<DocumentResult> Read(
        Guid projectId,
        string path,
        CancellationToken cancellationToken = default
    );
}

public sealed record DocumentResult(string? Content, string? Failure);
