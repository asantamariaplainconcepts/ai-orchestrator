using AiOrchestrator.Modules.Backlog.Domain;
using ErrorOr;

namespace AiOrchestrator.Modules.Backlog.Connectors;

/// <summary>
/// The single seam through which vendor backlogs are reached. No vendor SDK type appears in this
/// file or in anything that consumes it — that is what let a second vendor (DEC-045) slot in
/// without touching the polling loop, the mirror, or the API.
/// </summary>
interface IBacklogConnector
{
    BacklogVendor Vendor { get; }

    /// <summary>
    /// Confirms the credential can read the repository, before a Connector is stored.
    /// Distinguishes "repository not found" from "credential rejected" — the two have different
    /// fixes, so returning one generic failure would waste the operator's time.
    /// </summary>
    Task<ErrorOr<Success>> VerifyAccess(
        BacklogCoordinates coordinates,
        string token,
        CancellationToken cancellationToken
    );

    /// <summary>The repository's current open Stories.</summary>
    Task<ErrorOr<BacklogSnapshot>> FetchStories(
        BacklogCoordinates coordinates,
        string token,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// The seam's only writes (design D2 of label-write-back): labels are the one thing UC-008
    /// licenses the product to change at the vendor. Both are idempotent — applying a label the
    /// Story carries, or removing one it does not, is a no-op, not an error (design D3).
    /// </summary>
    Task<ErrorOr<Success>> ApplyLabel(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string label,
        string token,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Ensures a label exists in the <b>repository</b> — the seam's only method that names no
    /// Story. Until now the product could apply a label but never create one, which meant a
    /// trigger label nobody had used yet was not offerable in the vendor's own interface.
    /// <para>
    /// Idempotent in the strong sense: "already there" is success, so a caller never has to ask
    /// first. A vendor with no repository-level notion of a label succeeds without acting rather
    /// than manufacturing one by tagging an arbitrary work item — see the Azure DevOps
    /// implementation (automation-defaults design D3).
    /// </para>
    /// </summary>
    Task<ErrorOr<Success>> EnsureLabel(
        BacklogCoordinates coordinates,
        string label,
        string token,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// The change (pull request, in GitHub's dialect) that references this Story, or null when
    /// none does. Vendor-neutral by design (story-documents D1): a second vendor answers this
    /// from work-item relations, and "PullRequest" as a seam type would be a noun it has to
    /// pretend to speak.
    /// </summary>
    /// <summary>
    /// One Story as the vendor has it right now. The Mirror is refreshed by polling and is
    /// therefore stale immediately after a Run changes something — a write that must *replace*
    /// (the estimate label) has to read vendor truth, not our copy of it (BR-008).
    /// </summary>
    Task<ErrorOr<VendorStory?>> FetchStory(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string token,
        CancellationToken cancellationToken
    );

    /// <summary>Posts a comment on the Story — the Agent's answer, in the vendor's own thread.</summary>
    Task<ErrorOr<Success>> AddComment(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string comment,
        string token,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Sets the Story's state. A state the vendor does not accept is refused with a stated
    /// reason (design D4) — never guessed at, never silently ignored.
    /// </summary>
    Task<ErrorOr<Success>> SetState(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string state,
        string token,
        CancellationToken cancellationToken
    );

    Task<ErrorOr<LinkedChange?>> FindLinkedChange(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string token,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Every file the change touched, with the vendor's own unified patch (design D1/D2 of
    /// run-file-changes). The documents list is a projection of this — one vendor call, two
    /// consumers, rather than two calls against the same endpoint.
    /// </summary>
    Task<ErrorOr<IReadOnlyList<ChangedFile>>> ListChangeFiles(
        BacklogCoordinates coordinates,
        int changeNumber,
        string token,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// The Story's comments from a moment onwards, oldest first — read live, never mirrored
    /// (BR-008). Exists for resuming conversations (conversational-runs), so the shape of the
    /// question is "anything since the agent asked?", not "the whole history".
    /// </summary>
    Task<ErrorOr<IReadOnlyList<StoryComment>>> ReadComments(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        DateTimeOffset since,
        string token,
        CancellationToken cancellationToken
    );

    /// <summary>A document's content at a ref — read live, never mirrored (design D3).</summary>
    Task<ErrorOr<string>> ReadDocument(
        BacklogCoordinates coordinates,
        string path,
        string reference,
        string token,
        CancellationToken cancellationToken
    );

    Task<ErrorOr<Success>> RemoveLabel(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string label,
        string token,
        CancellationToken cancellationToken
    );
}

/// <summary>Where a backlog lives, in vendor-neutral terms.</summary>
readonly record struct BacklogCoordinates(string Owner, string Repository);

/// <summary>What a fetch returned. An empty list means the repository has no open Stories.</summary>
sealed record BacklogSnapshot(IReadOnlyList<VendorStory> Stories);

/// <summary>
/// One file a change touched. <see cref="Patch"/> is the vendor's unified diff, or null with a
/// <see cref="PatchOmitted"/> reason — a truncated patch shown as complete is the failure this
/// shape exists to prevent (design D3).
/// </summary>
sealed record ChangedFile(
    string Path,
    string Status,
    int Additions,
    int Deletions,
    string? Patch,
    PatchOmission? PatchOmitted
);

/// <summary>Why a diff cannot be shown — stated, never silently empty.</summary>
enum PatchOmission
{
    Binary = 1,
    TooLarge = 2,
}

/// <summary>
/// A change referencing a Story — the work written for it. <see cref="HeadRef"/> is what
/// documents are read at, so a branch that has moved on shows its current content.
/// </summary>
sealed record LinkedChange(int Number, string Title, string Url, string HeadRef);

/// <summary>One comment on a Story, as the seam speaks it.</summary>
sealed record StoryComment(string Body, DateTimeOffset CreatedAt);

/// <summary>A Story as the vendor reports it, in the product's field vocabulary (DEC-005).</summary>
sealed record VendorStory(
    string VendorId,
    string Title,
    string State,
    IReadOnlyList<string> Labels,
    /// <summary>The issue body as the vendor holds it — never sanitised at rest (design D2).</summary>
    string? Body = null
);
