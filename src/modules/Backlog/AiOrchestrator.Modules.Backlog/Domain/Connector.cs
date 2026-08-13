using AiOrchestrator.BuildingBlocks.Domain;
using AiOrchestrator.BuildingBlocks.Secrets;

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
        string? secretName,
        bool authenticatesAsHost
    )
    {
        ProjectId = projectId;
        Vendor = vendor;
        Owner = owner;
        Repository = repository;
        SecretName = secretName;
        AuthenticatesAsHost = authenticatesAsHost;
    }

    public Guid ProjectId { get; private set; }

    public BacklogVendor Vendor { get; private set; }

    public string Owner { get; private set; } = string.Empty;

    public string Repository { get; private set; } = string.Empty;

    /// <summary>
    /// The <b>name</b> of the secret holding the access token — never the value (BR-010). Null on
    /// the host path, where there is no secret and naming one that resolved to nothing would be
    /// worse than an absent name (DEC-069).
    /// </summary>
    public string? SecretName { get; private set; }

    /// <summary>
    /// Whether this Connector reaches the vendor as <b>the machine</b>, through the host's git
    /// credential helper (DEC-069 / ADR-0028). Stored explicitly rather than inferred from an
    /// absent <see cref="SecretName"/>: "no secret was named" and "authenticate as this host" are
    /// different states, and a deployment that inferred the second from the first would borrow an
    /// identity it must never have.
    /// </summary>
    public bool AuthenticatesAsHost { get; private set; }

    /// <summary>
    /// When this Connector's credential was last written by the product (#124). Null for a
    /// Connector that names a secret somebody else manages — the product genuinely does not know
    /// when that one was set, and saying "never" would be a lie rather than an absence.
    /// </summary>
    public DateTimeOffset? SecretSetAt { get; private set; }

    /// <summary>Records that the product itself wrote the value under <see cref="SecretName"/>.</summary>
    public void RecordSecretStored(DateTimeOffset at) => SecretSetAt = at;

    /// <summary>
    /// What this Connector's credential is, for the one seam that resolves it (BR-010, DEC-069).
    /// Lives here rather than at the call sites because there are two of them — the poller and
    /// <c>ConnectorAccess</c> — and two answers to "which source?" is exactly the drift
    /// <c>ConnectorAccess</c> was extracted to prevent.
    /// <para>
    /// Null means a Connector on the named path carrying no name: a corrupt row, refused as the
    /// missing secret it is. It must never fall through to the host path, which would borrow the
    /// machine's identity because a value was absent.
    /// </para>
    /// </summary>
    public CredentialReference? Credential() =>
        AuthenticatesAsHost ? CredentialReference.Host(VendorCredentialHosts.For(Vendor))
        : string.IsNullOrWhiteSpace(SecretName) ? null
        : CredentialReference.Named(SecretName);

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

    /// <summary>
    /// The command that makes a Local Run's fresh checkout buildable, run before the Agent starts
    /// (#332). Null means none, and none is a valid configuration — a checkout that needs no
    /// preparation is not a misconfigured one.
    /// <para>
    /// It lives here, beside the folder, and <b>never</b> in a file in the code source. On this lane
    /// the repository is what the Agent is editing, and the Agent runs as the machine owner with
    /// their environment and credentials: a repository file naming commands would let the Agent
    /// write it in one Run and have the next execute it. That is exactly why UC-031 requires
    /// per-version trust — a ceremony this field does not need, because nothing the Agent writes can
    /// become a command (design D1).
    /// </para>
    /// <para>
    /// Held on the Connector rather than per Automation: setup describes <b>the repository</b>, not
    /// the action being taken, so every Automation on the same folder needs the same tree. Per
    /// Automation would multiply one fact and let two of them disagree about how the same checkout
    /// is built.
    /// </para>
    /// </summary>
    public string? LocalSetupCommand { get; private set; }

    public void UseLocalFolder(string path, string? setupCommand = null)
    {
        CodeSource = CodeSource.LocalFolder;
        LocalPath = path;
        LocalSetupCommand = setupCommand;
    }

    public void UseRepositorySource()
    {
        CodeSource = CodeSource.Repository;
        LocalPath = null;

        // Cleared, not merely inapplicable: a stale command surviving a switch would be
        // configuration nobody can see, and a later switch back to the local folder would execute
        // it. Hiding and clearing are the same act (connector-configuration).
        LocalSetupCommand = null;
    }

    public static Connector Create(
        Guid projectId,
        BacklogVendor vendor,
        string owner,
        string repository,
        string secretName
    ) => new(projectId, vendor, owner, repository, secretName, authenticatesAsHost: false);

    /// <summary>
    /// A Connector that authenticates as its host — no secret, nothing written to the habitat's
    /// store (DEC-069). Composed only where the habitat is self-host; this type does not check the
    /// posture, because the caller is where that answer lives and a domain type that guessed it
    /// would be the wrong place to be wrong.
    /// </summary>
    public static Connector CreateOnHostCredential(
        Guid projectId,
        BacklogVendor vendor,
        string owner,
        string repository
    ) => new(projectId, vendor, owner, repository, secretName: null, authenticatesAsHost: true);

    /// <summary>Replaces the coordinates in place — a Project has at most one Connector.</summary>
    public void Reconfigure(
        BacklogVendor vendor,
        string owner,
        string repository,
        string? secretName,
        bool authenticatesAsHost = false
    )
    {
        // A different secret is a different credential, so what the product remembered about
        // when it wrote one no longer describes this Connector. Switching onto the host path says
        // the same thing more strongly: there is no stored value left to have a date.
        if (!string.Equals(SecretName, secretName, StringComparison.Ordinal) || authenticatesAsHost)
        {
            SecretSetAt = null;
        }

        Vendor = vendor;
        Owner = owner;
        Repository = repository;
        SecretName = secretName;
        AuthenticatesAsHost = authenticatesAsHost;

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

/// <summary>
/// Which host the machine's git credential helper is asked about for each vendor — one table, so
/// the host path exists for both vendors or for neither.
/// <para>
/// That symmetry is a requirement, not a convenience: DEC-045 promises a second vendor slots in
/// without touching the polling loop, the mirror or the API, and `connector-seam` forbids an
/// authentication mode available to one vendor alone. It is why DEC-069 chose the credential helper
/// over the `gh` CLI — both vendors have a helper, only one has a CLI.
/// </para>
/// </summary>
static class VendorCredentialHosts
{
    public static string For(BacklogVendor vendor) =>
        vendor switch
        {
            BacklogVendor.GitHub => "github.com",
            BacklogVendor.AzureDevOps => "dev.azure.com",
            _ => throw new ArgumentOutOfRangeException(
                nameof(vendor),
                vendor,
                "A vendor with no credential host cannot use the host path. Add it to this table "
                    + "rather than letting the resolution fall back to something."
            ),
        };
}

/// <summary>Where the Agent's working copy comes from (#210). Orthogonal to the vendor.</summary>
enum CodeSource
{
    /// <summary>The vendor's repository, cloned fresh per Run — today's behaviour.</summary>
    Repository = 1,

    /// <summary>A folder on the orchestrator's host (self-host flavour, DEC-049).</summary>
    LocalFolder = 2,
}
