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

    /// <summary>
    /// The prompt files a directory holds, read live from the repository's default branch (#229).
    /// Exists so setting a project up can find the pipeline it already has rather than installing
    /// a second copy of one — which is the same reason the grill reads its rubric from the project
    /// (DEC-048), one level up.
    /// <para>
    /// <see cref="DirectoryListing.Absent"/> distinguishes "nothing there" from a refusal, because
    /// a caller probing several candidate locations must not read an empty directory as a failure.
    /// </para>
    /// </summary>
    Task<DirectoryListing> ListPromptFiles(
        Guid projectId,
        string directory,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// What one candidate directory holds. <paramref name="Absent"/> is true when the directory is not
/// there at all — an ordinary answer while searching, and a different fact from
/// <paramref name="Failure"/>, which is the vendor refusing.
/// <para>
/// <paramref name="Subdirectories"/> is here because a pipeline is often kept one level down —
/// `ds-connect` keeps its commands in `.claude/commands/ds` — and a searcher that could not see
/// the child names would have to guess them, one repository read per guess.
/// </para>
/// </summary>
public sealed record DirectoryListing(
    string Directory,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Subdirectories,
    bool Absent,
    string? Failure
);

/// <summary>
/// <paramref name="ResolvedPath"/> is where the document was actually looked for. Present so a
/// caller that refuses on the *content* — an empty prompt (#150) — can name the same path the
/// reader's own failures name, instead of naming the file name an Admin typed.
/// </summary>
public sealed record DocumentResult(string? Content, string? Failure, string? ResolvedPath = null);
