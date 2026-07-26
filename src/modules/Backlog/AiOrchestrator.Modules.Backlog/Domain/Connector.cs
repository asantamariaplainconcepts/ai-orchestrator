using AiOrchestrator.BuildingBlocks.Domain;

namespace AiOrchestrator.Modules.Backlog.Domain;

/// <summary>
/// A Project's link to a vendor backlog (BC-002). One per Project.
/// <para>
/// <see cref="ProjectId"/> is a plain identifier, not a reference to the Projects module's type —
/// that is what keeps this module free of any cross-module assembly reference (design D2). The
/// cost is that no foreign key enforces the Project's existence; project deletion will have to
/// clean up here via a domain event when it arrives.
/// </para>
/// <para>
/// <see cref="SecretName"/> is the <b>name</b> of the secret holding the access token. The token
/// value is never stored (BR-010).
/// </para>
/// </summary>
sealed class Connector : Aggregate
{
    Connector() { }

    Connector(
        Guid projectId,
        BacklogVendor vendor,
        string owner,
        string repository,
        string secretName
    )
    {
        ProjectId = projectId;
        Vendor = vendor;
        Owner = owner;
        Repository = repository;
        SecretName = secretName;
    }

    public Guid ProjectId { get; private set; }

    public BacklogVendor Vendor { get; private set; }

    public string Owner { get; private set; } = string.Empty;

    public string Repository { get; private set; } = string.Empty;

    public string SecretName { get; private set; } = string.Empty;

    /// <summary>When the last poll succeeded. Null until one has.</summary>
    public DateTimeOffset? LastSyncedAt { get; private set; }

    /// <summary>
    /// Why the last poll failed, or null if it succeeded. Kept so the UI can tell "this backlog is
    /// empty" from "we could not read it" — the two look identical otherwise.
    /// </summary>
    public string? LastFailure { get; private set; }

    public DateTimeOffset? LastFailureAt { get; private set; }

    public static Connector Create(
        Guid projectId,
        BacklogVendor vendor,
        string owner,
        string repository,
        string secretName
    ) => new(projectId, vendor, owner, repository, secretName);

    /// <summary>Replaces the coordinates in place — a Project has at most one Connector.</summary>
    public void Reconfigure(
        BacklogVendor vendor,
        string owner,
        string repository,
        string secretName
    )
    {
        Vendor = vendor;
        Owner = owner;
        Repository = repository;
        SecretName = secretName;

        // The coordinates changed, so anything remembered about the old repository is meaningless.
        LastSyncedAt = null;
        LastFailure = null;
        LastFailureAt = null;
    }

    public void RecordSuccess(DateTimeOffset at)
    {
        LastSyncedAt = at;
        LastFailure = null;
        LastFailureAt = null;
    }

    public void RecordFailure(DateTimeOffset at, string reason)
    {
        LastFailure = reason;
        LastFailureAt = at;
    }
}

enum BacklogVendor
{
    GitHub = 1,
}
