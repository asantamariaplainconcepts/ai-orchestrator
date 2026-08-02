using AiOrchestrator.Modules.Backlog.Domain;

namespace AiOrchestrator.Modules.Backlog.Connectors;

/// <summary>
/// What this project's configuration will actually make the credential do (#226, design D1).
/// <para>
/// One function, two consumers: verification probes exactly this set, and the surface that tells
/// an Admin what to grant reads exactly this set. Two derivations would eventually disagree about
/// whether a local-folder project needs push — and the one that was wrong would either ask for a
/// permission nobody uses or refuse a correctly narrow token.
/// </para>
/// <para>
/// DEC-030 gave one PAT covering everything and said finer scoping was post-MVP. This is that:
/// breadth follows configuration, because a scope nobody exercises is a capability handed to a
/// prompt this product did not write (#162).
/// </para>
/// </summary>
static class ConnectorCapabilities
{
    /// <summary>
    /// The capabilities a Connector with this shape will use. Reads always; the writes the
    /// catalogue performs always; the code capability only where the product itself publishes —
    /// a local folder's working copy is the host's own and git runs with the host's credentials,
    /// so nothing here clones, pushes or opens a pull request (#210).
    /// </summary>
    public static IReadOnlyList<ConnectorCapability> For(CodeSource codeSource) =>
        codeSource == CodeSource.LocalFolder
            ? [.. Always]
            : [.. Always, ConnectorCapability.PublishChange];

    static readonly ConnectorCapability[] Always =
    [
        ConnectorCapability.ReadStories,
        ConnectorCapability.ReadDocuments,
        ConnectorCapability.WriteStory,
    ];
}

/// <summary>
/// One capability, named for what the product needs — never for a vendor's permission vocabulary,
/// which differs per vendor and which no caller should have to learn.
/// <para>
/// <see cref="GitHubScope"/> is the exception, and deliberately so: it is what a person selects
/// while minting a token, and it lives here so a capability cannot be added without saying what to
/// grant for it (design D4). A capability with no scope would be a documentation gap the compiler
/// cannot see, which is exactly the rot this pairing prevents.
/// </para>
/// </summary>
sealed record ConnectorCapability(string Name, string GitHubScope, bool IsWrite)
{
    /// <summary>
    /// What the document probe reads. Nearly arbitrary and deliberately so: absence is a pass, so
    /// this asks <i>may we read files here</i> rather than whether a particular file exists — a
    /// vendor checks permission before existence. `README.md` rather than the framework's own
    /// document, because pointing it at `docs/process/definition-of-ready.md` would read as though
    /// the answer depended on this repository's conventions, and it does not.
    /// </summary>
    public const string DocumentPath = "README.md";

    public static readonly ConnectorCapability ReadStories = new(
        "reading the backlog's Stories",
        "Issues: read",
        IsWrite: false
    );

    public static readonly ConnectorCapability ReadDocuments = new(
        "reading the repository's files",
        "Contents: read",
        IsWrite: false
    );

    public static readonly ConnectorCapability WriteStory = new(
        "labelling and commenting on a Story",
        "Issues: write",
        IsWrite: true
    );

    public static readonly ConnectorCapability PublishChange = new(
        "pushing a branch and opening a pull request",
        "Contents: write, Pull requests: write",
        IsWrite: true
    );
}
