namespace AiOrchestrator.BuildingBlocks.Secrets;

/// <summary>
/// Asks <b>this machine</b> what it is authenticated as for a vendor host — the git credential
/// helper, per read (DEC-069 / ADR-0028).
/// <para>
/// The helper rather than a vendor CLI, deliberately: both vendors have a helper and only one has
/// a CLI, and DEC-045's promise that a second vendor slots in without touching the polling loop,
/// the mirror or the API forbids an authentication mode available to GitHub alone.
/// </para>
/// <para>
/// Composed only where the habitat is self-host. A governed deployment composes no implementation
/// of this at all — the ability is <b>absent</b> there rather than present and refused, the same
/// distinction ADR-0021 draws for a terminal.
/// </para>
/// </summary>
public interface IHostCredentialResolver
{
    /// <summary>
    /// The credential this machine holds for <paramref name="credentialHost"/>.
    /// <para>
    /// <b>Resolution is non-interactive.</b> A helper that cannot answer without prompting fails
    /// carrying that reason rather than waiting, so a polling cycle can never stall on a credential
    /// prompt (UC-009). It never substitutes an empty or default credential, as no resolution may.
    /// </para>
    /// </summary>
    /// <exception cref="HostCredentialUnavailableException">
    /// The helper did not answer — it exited non-zero, timed out, wanted a human, or returned no
    /// password. Carries the reason, because BR-004 does not retry: whoever reads it is the retry.
    /// </exception>
    Task<HostCredential> Resolve(
        string credentialHost,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// What the helper answered. <see cref="Password"/> is the token both vendors accept where the
/// product already puts one; <see cref="Username"/> is carried for the record only.
/// </summary>
public sealed record HostCredential(string Password, string? Username);

/// <summary>
/// The machine could not say who it is for that host. Carries the host and the reason so the
/// operator learns which of the two to fix.
/// </summary>
public sealed class HostCredentialUnavailableException : Exception
{
    public HostCredentialUnavailableException(string credentialHost, string reason)
        : base(
            $"This machine's git credential helper did not supply a credential for "
                + $"'{credentialHost}': {reason}"
        )
    {
        CredentialHost = credentialHost;
        Reason = reason;
    }

    public HostCredentialUnavailableException() { }

    public HostCredentialUnavailableException(string message)
        : base(message) { }

    public HostCredentialUnavailableException(string message, Exception innerException)
        : base(message, innerException) { }

    public string? CredentialHost { get; }

    public string? Reason { get; }
}
