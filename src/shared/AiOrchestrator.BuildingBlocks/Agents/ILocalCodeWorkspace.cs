using ErrorOr;

namespace AiOrchestrator.BuildingBlocks.Agents;

/// <summary>
/// The workspace for a Run whose code source is a folder on this host (#210, self-host flavour —
/// DEC-049). The sibling of <see cref="ICodeWorkspace"/> behind the same layering: contract here,
/// git in host composition, selection per Run in the executor. Deliberately its own interface —
/// a folder has no coordinates and no token, and forcing it through the clone contract would
/// make both signatures lie.
/// </summary>
public interface ILocalCodeWorkspace
{
    /// <summary>
    /// What the host can say about one path: directory, git repository, current branch, clean
    /// tree. Answers about exactly the path it was given — it never lists contents. Serves the
    /// configuration-time validation and the pre-write dispatch refusal (BR-016) alike, so the
    /// two cannot disagree about what "clean" means.
    /// </summary>
    Task<PathInspection> Inspect(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the Run <b>its own checkout</b> of <paramref name="path"/> — a git worktree on
    /// <paramref name="branch"/>, in a product-owned directory outside the configured folder
    /// (#331, design D1/D3). The configured folder is not written to, checked out, or otherwise
    /// entered: whatever its owner has uncommitted there stays exactly as it is, which is why a
    /// clean tree is no longer required. The returned <see cref="LocalWorkspace.Path"/> is the
    /// checkout, not the folder.
    /// </summary>
    Task<ErrorOr<LocalWorkspace>> Prepare(
        string path,
        string branch,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Ends the Run's occupation of <b>its checkout</b>. On success: commits whatever changed in
    /// the checkout, then removes it — the branch remains in the owner's repository, because a
    /// worktree shares its refs, and the branch is the output (never pushed, never a PR). On
    /// failure: removes the checkout too. Nothing is restored, because nothing in the owner's
    /// folder was ever changed. Returns whether anything was committed.
    /// </summary>
    Task<ErrorOr<bool>> Conclude(
        LocalWorkspace workspace,
        string commitMessage,
        bool succeeded,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// The facts validation reports. Null branch/clean when it is not a repository.
/// <para>
/// <paramref name="OriginUrl"/> is what lets a named folder answer for itself which vendor and which
/// coordinates a Project should use (#347) — null when the folder is not a repository or has no
/// `origin`, which the caller must tell apart from a remote it simply could not parse.
/// </para>
/// </summary>
public sealed record PathInspection(
    bool IsDirectory,
    bool IsGitRepository,
    string? Branch,
    bool? IsClean,
    string? OriginUrl = null
);

/// <summary>
/// A prepared local run: the checkout it works in, the branch that is its output, and the folder
/// that checkout came from. There is no "way back" — the owner's folder was never left (#331).
/// </summary>
public sealed record LocalWorkspace(string Path, string Branch, string Folder);

/// <summary>Stage-named refusals, the <see cref="WorkspaceErrors"/> pattern.</summary>
public static class LocalWorkspaceErrors
{
    public static Error NotARepository(string path) =>
        Error.Validation(
            "LocalWorkspace.NotARepository",
            $"'{path}' is not a git repository — a local run needs one to branch in."
        );

    /// <summary>
    /// The checkout could not be created (#331). Carries the folder <b>and git's own reason</b>,
    /// because BR-004 does not retry: whoever reads this is the retry, and "the worktree could
    /// not be created" tells them nothing they can act on.
    /// </summary>
    public static Error CheckoutFailed(string path, string detail) =>
        Error.Failure(
            "LocalWorkspace.CheckoutFailed",
            $"A checkout of '{path}' could not be created for this run: {detail}"
        );

    public static Error BranchFailed(string detail) =>
        Error.Failure("LocalWorkspace.BranchFailed", $"Creating the run branch failed: {detail}");

    public static Error CommitFailed(string detail) =>
        Error.Failure(
            "LocalWorkspace.CommitFailed",
            $"Committing the run's changes failed: {detail}"
        );
}
