namespace AiOrchestrator.BuildingBlocks.Secrets;

/// <summary>
/// The one seam a Connector's credential is resolved through, <b>per read</b>, whichever source it
/// comes from (BR-010; DEC-069).
/// <para>
/// It exists because <see cref="ISecretResolver"/> resolves by <i>name</i> and a host-resolved
/// credential has none. Making the host path a second <see cref="ISecretResolver"/> would have
/// meant encoding a host in a secret name — stringly-typed, and unenforceable at the boundary that
/// matters. ADR-0028 anticipated exactly this shape: "a host-derived credential is another resolver
/// behind that seam, not a change to the Connector's fourteen signatures".
/// </para>
/// <para>
/// Returns the value <b>with</b> its <see cref="CredentialSource"/>, together, because a caller
/// that had to ask a second question to learn what it authenticated as would eventually not ask.
/// </para>
/// </summary>
public interface IConnectorCredentialResolver
{
    /// <summary>
    /// Resolves <paramref name="reference"/> now — no cache, so a rotated secret and a refreshed
    /// helper credential are both picked up without a restart.
    /// <para>
    /// Never returns null, empty or a default: a silently empty credential fails far from its
    /// cause, and on the host path it would fail as "the vendor rejected you" rather than "this
    /// machine is not logged in".
    /// </para>
    /// </summary>
    Task<ResolvedCredential> Resolve(
        CredentialReference reference,
        CancellationToken cancellationToken = default
    );
}

/// <summary>The token to send, and the honest answer to "as whom?".</summary>
public sealed record ResolvedCredential(string Token, CredentialSource Source);
