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

    /// <summary>
    /// The project's Stories that carry the <b>hold</b> — the reserved label <c>hitl</c>, meaning a
    /// person must act before anything else does (BR-007, DEC-067).
    /// <para>
    /// Its own member rather than <see cref="VendorStoryIds"/> plus a <see cref="Find"/> per id
    /// (#335): that shape is one round trip for every Story in the mirror, and the sidebar tree asks
    /// this question for every visible project on the shell's polling cadence. The inbox can afford
    /// per-entry lookups because its list is already the answer; this is a filter over the whole
    /// mirror, which is a different question.
    /// </para>
    /// <para>
    /// Title travels with the id because every caller renders one: a held Story shown as a bare
    /// vendor id answers "which #491?" with silence, the same reason the inbox carries its
    /// Project's name.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<HeldStory>> Held(
        Guid projectId,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// A Story waiting on a person, identified and named. Deliberately narrower than
/// <see cref="StorySnapshot"/>: the hold's consumers render a row, and carrying the whole body
/// through a per-project read that runs on a polling cadence would move a requirement's full text
/// for nothing.
/// </summary>
public sealed record HeldStory(string VendorStoryId, string Title);

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
