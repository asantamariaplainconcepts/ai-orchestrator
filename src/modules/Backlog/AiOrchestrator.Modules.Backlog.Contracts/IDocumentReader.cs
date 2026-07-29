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

    /// <summary>
    /// The same read for a prompt the project owns, given only its <b>name</b> — the directory is a
    /// Connector setting and resolving it stays inside the module that holds it (#150, design D6).
    /// Callers pass a name and never learn a directory exists, so there is one site that composes the
    /// path and therefore one path a failure can name.
    /// </summary>
    Task<DocumentResult> ReadPrompt(
        Guid projectId,
        string name,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// <paramref name="ResolvedPath"/> is where the document was actually looked for. Present so a
/// caller that refuses on the *content* — an empty prompt (#150) — can name the same path the
/// reader's own failures name, instead of naming the file name an Admin typed.
/// </summary>
public sealed record DocumentResult(string? Content, string? Failure, string? ResolvedPath = null);
