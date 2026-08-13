using AiOrchestrator.BuildingBlocks.Secrets;

namespace AiOrchestrator.ServiceDefaults.Secrets;

/// <summary>
/// The one seam, with its two sources behind it (BR-010's "one abstraction, per read"; DEC-069).
/// <para>
/// <paramref name="host"/> is <c>null</c> wherever the habitat cannot authenticate as its machine —
/// a governed deployment composes no host resolver at all, so the host path is <b>absent</b> there
/// rather than present and refused. A Connector that somehow names it in such a deployment is a
/// configuration that cannot be honoured, and says so in those words rather than failing at the
/// vendor as an authorization problem.
/// </para>
/// </summary>
sealed class ConnectorCredentialResolver(
    ISecretResolver secrets,
    IHostCredentialResolver? host = null
) : IConnectorCredentialResolver
{
    public async Task<ResolvedCredential> Resolve(
        CredentialReference reference,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (!reference.IsHostResolved)
        {
            var secretName = reference.SecretName!;
            var value = await secrets.Resolve(secretName, cancellationToken);

            return new ResolvedCredential(value, CredentialSource.NamedSecret(secretName));
        }

        var credentialHost = reference.CredentialHost!;

        if (host is null)
        {
            throw new HostCredentialUnavailableException(
                credentialHost,
                "this deployment does not authenticate as its host — a governed deployment names a "
                    + "credential instead (DEC-069)"
            );
        }

        var answered = await host.Resolve(credentialHost, cancellationToken);

        return new ResolvedCredential(
            answered.Password,
            CredentialSource.HostCredentialHelper(credentialHost, answered.Username)
        );
    }
}
