namespace AiOrchestrator.BuildingBlocks.Secrets;

/// <summary>
/// What a Connector's credential <b>is</b>, rather than what it says: either the name of a secret
/// in the habitat's own store, or the host itself (DEC-069 / ADR-0028 — a self-host deployment MAY
/// reach the vendor as the machine; a governed deployment MAY NOT).
/// <para>
/// The two are exclusive <b>by construction</b>: there is no constructor that takes both and none
/// that takes neither, so "a name and a host" and "silently the host because nothing was set" are
/// states this type cannot represent. That matters more here than elsewhere — an absent reference
/// defaulting to the host path would authenticate as the machine on a deployment that must never
/// do so.
/// </para>
/// <para>
/// BR-010's "one abstraction, per read" is kept literally: this is the argument
/// <see cref="IConnectorCredentialResolver"/> takes, not a second resolver beside
/// <see cref="ISecretResolver"/>. The fourteen <c>IBacklogConnector</c> methods keep their
/// <c>string token</c> — resolution happens before them.
/// </para>
/// </summary>
public sealed record CredentialReference
{
    CredentialReference(string? secretName, string? credentialHost)
    {
        SecretName = secretName;
        CredentialHost = credentialHost;
    }

    /// <summary>The secret whose value the habitat's store holds. Null on the host path.</summary>
    public string? SecretName { get; }

    /// <summary>
    /// The vendor host the machine's git credential helper is asked about (<c>github.com</c>,
    /// <c>dev.azure.com</c>). Null when a secret is named.
    /// </summary>
    public string? CredentialHost { get; }

    /// <summary>True when the credential comes from the machine rather than from the store.</summary>
    public bool IsHostResolved => CredentialHost is not null;

    /// <summary>A Connector that names a secret — every habitat, unchanged behaviour.</summary>
    public static CredentialReference Named(string secretName) =>
        string.IsNullOrWhiteSpace(secretName)
            ? throw new ArgumentException(
                "A named credential needs a secret name. An empty name would resolve to nothing "
                    + "and read as a missing credential rather than a missing configuration.",
                nameof(secretName)
            )
            : new CredentialReference(secretName, credentialHost: null);

    /// <summary>
    /// A Connector that authenticates as its host. Composed only where the habitat can do so —
    /// the posture check belongs to the caller, because this type cannot know which habitat it is
    /// in and a type that guessed would be the wrong place to be wrong.
    /// </summary>
    public static CredentialReference Host(string credentialHost) =>
        string.IsNullOrWhiteSpace(credentialHost)
            ? throw new ArgumentException(
                "The host path needs the vendor host to ask the credential helper about.",
                nameof(credentialHost)
            )
            : new CredentialReference(secretName: null, credentialHost);
}

/// <summary>
/// What actually touched the vendor, so the source is never left to inference — the shape
/// <c>IAgentProcessHost.CredentialSource</c> already uses for the agent's own process, borrowed
/// rather than reinvented (ADR-0028; BR-014).
/// </summary>
public sealed record CredentialSource(string Kind, string Detail)
{
    public const string NamedSecretKind = "secret";
    public const string HostHelperKind = "host-credential-helper";

    public static CredentialSource NamedSecret(string secretName) =>
        new(NamedSecretKind, secretName);

    /// <summary>
    /// Names the helper and the host it was asked about. The username is recorded because it is
    /// what a reader needs to answer "as whom?" — never because it decides an authorization
    /// header (the helper's password is the token; see the resolver).
    /// </summary>
    public static CredentialSource HostCredentialHelper(string host, string? username) =>
        new(HostHelperKind, username is null ? host : $"{username}@{host}");

    /// <summary>One line for a log or an audit record, in the vocabulary a person reads.</summary>
    public string Describe() =>
        Kind == HostHelperKind
            ? $"the host's git credential helper ({Detail})"
            : $"the secret named '{Detail}'";
}
