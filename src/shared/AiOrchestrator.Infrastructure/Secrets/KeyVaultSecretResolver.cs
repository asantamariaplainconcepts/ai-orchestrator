using AiOrchestrator.BuildingBlocks.Secrets;
using Azure;
using Azure.Security.KeyVault.Secrets;

namespace AiOrchestrator.ServiceDefaults.Secrets;

/// <summary>
/// Deployed resolver: reads secrets from Azure Key Vault by name.
/// <para>
/// Same contract as <see cref="ConfigurationSecretResolver"/> — resolve per call, throw
/// <see cref="SecretNotFoundException"/> when the name is absent — which is what lets the host
/// swap them with no call site knowing. Nothing here caches: a rotated secret must be picked up
/// without a restart, and the client's own pipeline handles retries and telemetry.
/// </para>
/// <para>
/// A vault name that exists but that this identity cannot read surfaces as a
/// <see cref="RequestFailedException"/>, deliberately not translated to "not found": a
/// permission problem and a missing secret have different fixes, and collapsing them sends the
/// next operator looking in the wrong place.
/// </para>
/// </summary>
public sealed class KeyVaultSecretResolver(SecretClient client) : ISecretResolver
{
    public async Task<string> Resolve(
        string secretName,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        try
        {
            var secret = await client.GetSecretAsync(
                secretName,
                cancellationToken: cancellationToken
            );
            var value = secret.Value.Value;

            // A secret whose value is empty is a misconfiguration wearing the costume of a
            // working one; treat it as absent rather than handing an empty credential onward.
            return string.IsNullOrEmpty(value)
                ? throw new SecretNotFoundException(secretName)
                : value;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            throw new SecretNotFoundException(secretName);
        }
    }
}
