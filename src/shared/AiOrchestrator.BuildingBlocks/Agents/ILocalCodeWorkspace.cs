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
    /// Re-verifies the tree is clean (the dispatch check races with a human typing in that
    /// folder), remembers the current ref, and creates + switches to <paramref name="branch"/>.
    /// </summary>
    Task<ErrorOr<LocalWorkspace>> Prepare(
        string path,
        string branch,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Ends the run's occupation of the folder. On success: commits whatever changed and leaves
    /// the run branch checked out — the branch is the output (never pushed, never a PR). On
    /// failure: restores the previously checked-out ref so the owner finds their folder as they
    /// left it. Returns whether anything was committed.
    /// </summary>
    Task<ErrorOr<bool>> Conclude(
        LocalWorkspace workspace,
        string commitMessage,
        bool succeeded,
        CancellationToken cancellationToken = default
    );
}

/// <summary>The four facts validation reports. Null branch/clean when it is not a repository.</summary>
public sealed record PathInspection(
    bool IsDirectory,
    bool IsGitRepository,
    string? Branch,
    bool? IsClean
);

/// <summary>A prepared local run: the folder, the run branch, and the way back.</summary>
public sealed record LocalWorkspace(string Path, string Branch, string PreviousRef);

/// <summary>Stage-named refusals, the <see cref="WorkspaceErrors"/> pattern.</summary>
public static class LocalWorkspaceErrors
{
    public static Error NotARepository(string path) =>
        Error.Validation(
            "LocalWorkspace.NotARepository",
            $"'{path}' is not a git repository — a local run needs one to branch in."
        );

    public static Error DirtyTree(string path) =>
        Error.Validation(
            "LocalWorkspace.DirtyTree",
            $"The folder '{path}' has uncommitted changes — commit or stash them first."
        );

    public static Error BranchFailed(string detail) =>
        Error.Failure("LocalWorkspace.BranchFailed", $"Creating the run branch failed: {detail}");

    public static Error CommitFailed(string detail) =>
        Error.Failure(
            "LocalWorkspace.CommitFailed",
            $"Committing the run's changes failed: {detail}"
        );
}
