using AiOrchestrator.Modules.Backlog.Domain;
using ErrorOr;

namespace AiOrchestrator.Modules.Backlog.Connectors;

/// <summary>
/// The single seam through which vendor backlogs are reached. No vendor SDK type appears in this
/// file or in anything that consumes it — that is what lets a second vendor (OPN-003) slot in
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
    /// The change (pull request, in GitHub's dialect) that references this Story, or null when
    /// none does. Vendor-neutral by design (story-documents D1): a second vendor answers this
    /// from work-item relations, and "PullRequest" as a seam type would be a noun it has to
    /// pretend to speak.
    /// </summary>
    Task<ErrorOr<LinkedChange?>> FindLinkedChange(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string token,
        CancellationToken cancellationToken
    );

    /// <summary>The markdown documents the change adds or modifies — deletions excluded.</summary>
    Task<ErrorOr<IReadOnlyList<string>>> ListChangeDocuments(
        BacklogCoordinates coordinates,
        int changeNumber,
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
/// A change referencing a Story — the work written for it. <see cref="HeadRef"/> is what
/// documents are read at, so a branch that has moved on shows its current content.
/// </summary>
sealed record LinkedChange(int Number, string Title, string Url, string HeadRef);

/// <summary>A Story as the vendor reports it, in the product's field vocabulary (DEC-005).</summary>
sealed record VendorStory(
    string VendorId,
    string Title,
    string State,
    IReadOnlyList<string> Labels,
    /// <summary>The issue body as the vendor holds it — never sanitised at rest (design D2).</summary>
    string? Body = null
);
