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

    /// <summary>
    /// When this Connector's credential was last written by the product (#124). Null for a
    /// Connector that names a secret somebody else manages — the product genuinely does not know
    /// when that one was set, and saying "never" would be a lie rather than an absence.
    /// </summary>
    public DateTimeOffset? SecretSetAt { get; private set; }

    /// <summary>Records that the product itself wrote the value under <see cref="SecretName"/>.</summary>
    public void RecordSecretStored(DateTimeOffset at) => SecretSetAt = at;

    /// <summary>When the last poll succeeded. Null until one has.</summary>
    public DateTimeOffset? LastSyncedAt { get; private set; }

    /// <summary>
    /// Why the last poll failed, or null if it succeeded. Kept so the UI can tell "this backlog is
    /// empty" from "we could not read it" — the two look identical otherwise.
    /// </summary>
    public string? LastFailure { get; private set; }

    public DateTimeOffset? LastFailureAt { get; private set; }

    /// <summary>
    /// The name of the secret the vendor signs webhooks with — a name, never the value
    /// (BR-010). Null means this Connector does not accept webhooks; polling still runs.
    /// </summary>
    public string? WebhookSecretName { get; private set; }

    public void UseWebhookSecret(string? secretName) => WebhookSecretName = secretName;

    /// <summary>
    /// Where the code lives, when that is not where the backlog lives. Empty for GitHub, whose
    /// issues and code share a repository; on Azure DevOps it names the repository inside the
    /// project that the implement-to-PR action clones (design D5).
    /// </summary>
    public string? CodeRepository { get; private set; }

    public void UseCodeRepository(string? repository) => CodeRepository = repository;

    /// <summary>
    /// Where the project's prompt files live inside the repository — the same kind of fact as
    /// <see cref="CodeRepository"/>, one level in (#150, design D6). Null means the default, so a
    /// project that configures nothing still resolves prompt names.
    /// <para>
    /// Held once here rather than on each Automation: a team that moves its prompts changes one
    /// field, and every Automation follows on its next Run because the file is read live and no copy
    /// was ever kept.
    /// </para>
    /// </summary>
    public string? PromptDirectory { get; private set; }

    public void UsePromptDirectory(string? directory) => PromptDirectory = directory;

    /// <summary>
    /// Where the Agent's working copy comes from (#210). The backlog vendor and the code source
    /// are two facts: Stories always come from the vendor; only the code may be a folder on the
    /// host, in the self-host flavour (DEC-049). Every Connector configured before this field
    /// existed reads as <see cref="Domain.CodeSource.Repository"/> and behaves as before.
    /// </summary>
    public CodeSource CodeSource { get; private set; } = CodeSource.Repository;

    /// <summary>
    /// The absolute path on the host, when <see cref="CodeSource"/> is a local folder. Null
    /// otherwise — a stale path surviving a switch back to Repository would look configured.
    /// </summary>
    public string? LocalPath { get; private set; }

    public void UseLocalFolder(string path)
    {
        CodeSource = CodeSource.LocalFolder;
        LocalPath = path;
    }

    public void UseRepositorySource()
    {
        CodeSource = CodeSource.Repository;
        LocalPath = null;
    }

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
        // A different secret is a different credential, so what the product remembered about
        // when it wrote one no longer describes this Connector.
        if (!string.Equals(SecretName, secretName, StringComparison.Ordinal))
        {
            SecretSetAt = null;
        }

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

    /// <summary>
    /// Work items in an organisation/project; tags where GitHub has labels; a state vocabulary
    /// and estimate field that depend on the project's process template (DEC-011, OPN-003).
    /// </summary>
    AzureDevOps = 2,
}

/// <summary>Where the Agent's working copy comes from (#210). Orthogonal to the vendor.</summary>
enum CodeSource
{
    /// <summary>The vendor's repository, cloned fresh per Run — today's behaviour.</summary>
    Repository = 1,

    /// <summary>A folder on the orchestrator's host (self-host flavour, DEC-049).</summary>
    LocalFolder = 2,
}
