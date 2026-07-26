using ErrorOr;

namespace AiOrchestrator.BuildingBlocks.Agents;

/// <summary>
/// The ceremony as a seam (agent-implements-pr design D1): clone, branch, commit, push and PR
/// are deterministic code — the Agent's job is the implementation, never the mechanics. Each
/// refusal names its stage (design D4): clone auth, push rejection and PR refusal have four
/// different fixes, and one generic message would collapse them into guessing.
/// </summary>
public interface ICodeWorkspace
{
    /// <summary>Clones the repository with the token and checks out branch <c>run/&lt;id&gt;</c>.</summary>
    Task<ErrorOr<PreparedWorkspace>> Prepare(
        CodeCoordinates coordinates,
        Guid runId,
        string token,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Commits whatever changed, pushes the run branch, and opens the pull request. An empty
    /// change set is a refusal, not an empty PR (design D3).
    /// </summary>
    Task<ErrorOr<PublishedChange>> Publish(
        PreparedWorkspace workspace,
        string title,
        string body,
        string token,
        CancellationToken cancellationToken
    );
}

/// <summary>Where the project's code lives, in vendor-neutral terms (DEC-030: one PAT reaches it).</summary>
public sealed record CodeCoordinates(string Owner, string Repository);

public sealed record PreparedWorkspace(CodeCoordinates Coordinates, string Path, string Branch);

public sealed record PublishedChange(string PullRequestUrl);

/// <summary>The closed set of stage-named refusals both implementations and tests speak.</summary>
public static class WorkspaceErrors
{
    public static Error CloneFailed(string detail) =>
        Error.Failure("Workspace.CloneFailed", $"Cloning the repository failed: {detail}");

    public static Error NoChanges() =>
        Error.Validation(
            "Workspace.NoChanges",
            "The agent produced no file changes — nothing to publish, and an empty pull "
                + "request would pretend otherwise."
        );

    public static Error PushFailed(string detail) =>
        Error.Failure("Workspace.PushFailed", $"Pushing the run branch failed: {detail}");

    public static Error PullRequestFailed(string detail) =>
        Error.Failure("Workspace.PullRequestFailed", $"Opening the pull request failed: {detail}");
}
