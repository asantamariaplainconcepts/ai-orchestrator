namespace AiOrchestrator.BuildingBlocks.Secrets;

/// <summary>
/// Writes a secret value under a name — and nothing else (#124, design D1).
/// <para>
/// Deliberately a sibling of <see cref="ISecretResolver"/> rather than two more methods on it.
/// The property that matters is that almost nothing in this product can write a credential, and
/// the dependency graph is the only place that property can be stated where it cannot be
/// forgotten: a component that takes <see cref="ISecretResolver"/> is structurally unable to
/// store, and one that takes this is visible in a review as something that can.
/// </para>
/// <para>
/// There is no read. Not "a read that throws", not "a read behind a flag" — no method. That is
/// the strongest guarantee available for "a stored value never comes back out", because it
/// cannot be forgotten under deadline the way a rule in a document can.
/// </para>
/// </summary>
public interface ISecretStore
{
    /// <summary>
    /// Writes <paramref name="value"/> under <paramref name="secretName"/>, replacing whatever
    /// was there. Throws <see cref="SecretStoreUnavailableException"/> in a habitat that cannot
    /// accept values — never returns as though it had written.
    /// </summary>
    Task Store(string secretName, string value, CancellationToken cancellationToken = default);
}

/// <summary>
/// This habitat has nowhere to put a value. Carries what to do instead, because "storing is not
/// available here" without the alternative leaves the reader with no next step.
/// </summary>
public sealed class SecretStoreUnavailableException : Exception
{
    public SecretStoreUnavailableException(string remedy)
        : base($"This deployment cannot store secret values. {remedy}") => Remedy = remedy;

    public SecretStoreUnavailableException()
        : this("Configure a secret store, or name a secret that already exists.") { }

    public SecretStoreUnavailableException(string message, Exception innerException)
        : base(message, innerException) { }

    public string? Remedy { get; }
}

/// <summary>
/// The habitat with no writable store: development and tests reading `Secrets:` from
/// configuration, where values arrive as environment variables the product does not own.
/// <para>
/// Registered rather than left absent so that the refusal is the seam's own, phrased once, with
/// the remedy in it. An absent registration would surface as a dependency-injection failure —
/// true, useless, and impossible to render in a form.
/// </para>
/// </summary>
public sealed class UnavailableSecretStore(string remedy) : ISecretStore
{
    /// <summary>
    /// How to gain the ability, readable without provoking the refusal (#222). The capabilities
    /// read reports it so a form can state the remedy instead of offering a control whose only
    /// outcome is the exception below.
    /// </summary>
    public string Remedy { get; } = remedy;

    public Task Store(
        string secretName,
        string value,
        CancellationToken cancellationToken = default
    ) => throw new SecretStoreUnavailableException(Remedy);
}
