using AiOrchestrator.Modules.Backlog.Domain;
using ErrorOr;

namespace AiOrchestrator.Modules.Backlog.Connectors;

/// <summary>
/// What a credential can actually do, one answer per capability (#132, design D2; widened by
/// #226).
/// <para>
/// The capabilities are named after what the product needs, not after a vendor's permission
/// vocabulary: those differ per vendor, and the caller must not learn either. Which ones are
/// asked about follows the Connector's configuration (<see cref="ConnectorCapabilities"/>) — a
/// project whose code lives in a local folder is never asked whether it may push.
/// </para>
/// <para>
/// A verdict is satisfied when nothing was <b>refused</b>. Not-verifiable is not a refusal: a
/// vendor that will not answer without acting has told us nothing, and blocking a save on that
/// would reject correct credentials, while calling it a pass would manufacture confidence nobody
/// earned (#226, design D2).
/// </para>
/// </summary>
sealed record CredentialVerdict(IReadOnlyList<CapabilityResult> Capabilities)
{
    public bool Satisfied => Capabilities.All(capability => capability.Failure is null);

    /// <summary>
    /// The first refusal, for a caller that must fail with one reason — in the order the
    /// capabilities were probed, which starts with the reads: a credential that cannot see the
    /// backlog at all makes every later answer uninteresting.
    /// </summary>
    public Error FirstRefusal =>
        Capabilities
            .Select(capability => capability.Failure)
            .FirstOrDefault(failure => failure is not null)
        ?? throw new InvalidOperationException(
            "FirstRefusal read from a verdict where nothing was refused."
        );

    public static CredentialVerdict Of(params CapabilityResult[] results) => new(results);
}

/// <summary>
/// One capability's answer. A failure carries the error the vendor's translation produced, so the
/// reason reaching the operator is the vendor's own rather than ours.
/// <para>
/// <see cref="Unverifiable"/> is the third outcome (#226): the vendor cannot say whether this is
/// permitted without performing it, and verification writes nothing, in any habitat. It carries
/// its reason and does not block a save.
/// </para>
/// </summary>
sealed record CapabilityResult(string Name, Error? Failure, string? Unverifiable = null)
{
    public bool Succeeded => Failure is null;

    public static CapabilityResult Passed(string name) => new(name, null);

    public static CapabilityResult Refused(string name, Error failure) => new(name, failure);

    public static CapabilityResult NotVerifiable(string name, string reason) =>
        new(name, null, reason);
}
