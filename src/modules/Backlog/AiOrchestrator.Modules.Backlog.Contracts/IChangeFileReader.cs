namespace AiOrchestrator.Modules.Backlog.Contracts;

/// <summary>
/// The files a Story's linked change touched, for modules that must not reach the Backlog
/// implementation (run-file-changes design D4 — the Runs module reads through Contracts, as it
/// does for Stories and Connectors).
/// </summary>
public interface IChangeFileReader
{
    /// <summary>
    /// Null when the Story has no linked change at all — distinct from an empty list, which
    /// means the change touched nothing.
    /// </summary>
    Task<ChangeFiles?> ForStory(
        Guid projectId,
        string vendorStoryId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// The same view for a change known by number (run-on-a-pr): a change-targeted Run already
    /// holds its number, so resolving through a Story it does not have would be a detour through
    /// an absence. Null when the change cannot be read.
    /// </summary>
    Task<ChangeFiles?> ForChange(
        Guid projectId,
        int changeNumber,
        CancellationToken cancellationToken = default
    );
}

public sealed record ChangeFiles(int Number, string Url, IReadOnlyList<ChangedFileView> Files);

/// <summary>
/// <see cref="Patch"/> is null when <see cref="PatchOmittedReason"/> says why — "Binary" or
/// "TooLarge". A truncated patch presented as complete would let a reviewer approve half a
/// change believing they saw all of it.
/// </summary>
public sealed record ChangedFileView(
    string Path,
    string Status,
    int Additions,
    int Deletions,
    string? Patch,
    string? PatchOmittedReason
);
