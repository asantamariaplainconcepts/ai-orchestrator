using AiOrchestrator.Modules.Backlog.Domain;
using ErrorOr;

namespace AiOrchestrator.Modules.Backlog.Connectors;

/// <summary>
/// What a credential can actually do, one answer per capability (#132, design D2).
/// <para>
/// The capabilities are named after what the product needs, not after a vendor's permission
/// vocabulary: those differ per vendor, and the caller must not learn either. Reading Stories is
/// what matching and the mirror need; reading a document is what every conversational action
/// needs. A credential that can do one and not the other cannot run the pipeline, which is the
/// question UC-004 asks.
/// </para>
/// </summary>
sealed record CredentialVerdict(CapabilityResult Stories, CapabilityResult Documents)
{
    public bool Satisfied => Stories.Succeeded && Documents.Succeeded;

    /// <summary>
    /// The first refusal, for a caller that must fail with one reason. Stories first because a
    /// credential that cannot see the backlog at all makes the document answer uninteresting.
    /// </summary>
    public Error FirstRefusal =>
        Stories.Failure
        ?? Documents.Failure
        ?? throw new InvalidOperationException(
            "FirstRefusal read from a verdict where every capability succeeded."
        );

    public static CredentialVerdict Of(CapabilityResult stories, CapabilityResult documents) =>
        new(stories, documents);
}

/// <summary>
/// One capability's answer. A failure carries the error the vendor's translation produced, so the
/// reason reaching the operator is the vendor's own rather than ours.
/// </summary>
sealed record CapabilityResult(string Name, Error? Failure)
{
    public bool Succeeded => Failure is null;

    public static CapabilityResult Passed(string name) => new(name, null);

    public static CapabilityResult Refused(string name, Error failure) => new(name, failure);
}

/// <summary>The names the product uses for what it needs to read. Rendered to operators.</summary>
static class Capabilities
{
    public const string Stories = "reading the backlog's Stories";

    public const string Documents = "reading the repository's files";
}

/// <summary>
/// What the probe asks for when it tests whether files are readable (#132).
/// <para>
/// The path is nearly arbitrary and that is deliberate: absence is a pass (design D6), so this
/// tests <i>may we read files here</i> rather than whether any particular file exists. A vendor
/// checks permission before existence, so a credential without file access is refused whether or
/// not the path is there.
/// </para>
/// <para>
/// `README.md` rather than the framework's own document, because pointing the probe at
/// `docs/process/definition-of-ready.md` would read as though the answer depended on this
/// repository's conventions, and it does not.
/// </para>
/// </summary>
static class ConnectorProbe
{
    public const string DocumentPath = "README.md";
}
