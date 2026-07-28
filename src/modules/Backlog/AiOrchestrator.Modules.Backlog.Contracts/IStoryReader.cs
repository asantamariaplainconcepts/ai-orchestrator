namespace AiOrchestrator.Modules.Backlog.Contracts;

/// <summary>
/// The read surface for mirrored Stories. <see cref="StoryChanged"/> deliberately carries
/// identity only; a consumer that needs labels or state reads current truth here (BR-008 — the
/// vendor is the source of truth, and the mirror is its local copy).
/// </summary>
public interface IStoryReader
{
    /// <summary>The Story's current snapshot, or null when the mirror no longer holds it.</summary>
    Task<StorySnapshot?> Find(
        Guid projectId,
        string vendorStoryId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Every Story the mirror currently holds for the project, as vendor ids. Ids only: the
    /// pulse's coverage figure (#108) needs set arithmetic, not snapshots, and a consumer that
    /// needs more reads <see cref="Find"/> per id like the inbox does.
    /// </summary>
    Task<IReadOnlyList<string>> VendorStoryIds(
        Guid projectId,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Current truth at read time — labels and state in the vendor's own vocabulary.</summary>
public sealed record StorySnapshot(
    Guid ProjectId,
    string VendorStoryId,
    string Title,
    string State,
    IReadOnlyList<string> Labels,
    /// <summary>The requirement itself — what an Agent needs to implement anything (design D3).</summary>
    string? Body
);
