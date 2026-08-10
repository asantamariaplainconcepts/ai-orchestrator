using AiOrchestrator.BuildingBlocks.Secrets;
using Azure;
using Azure.Security.KeyVault.Secrets;

namespace AiOrchestrator.ServiceDefaults.Secrets;

/// <summary>
/// The deployed habitat's store: writes the value into Azure Key Vault under the given name.
/// <para>
/// Setting an existing name creates a new version rather than overwriting, which is what makes
/// rotation safe and auditable — the vault keeps the history, and the resolver reads the current
/// version because it resolves per read (the seam's original contract).
/// </para>
/// <para>
/// A 403 is translated, deliberately: the identity can reach the vault but may not write to it,
/// which is a Terraform problem and not a caller's mistake. Leaving it as a raw
/// <see cref="RequestFailedException"/> sends the next operator to the wrong place.
/// </para>
/// </summary>
public sealed class KeyVaultSecretStore(SecretClient client) : ISecretStore
{
    public async Task Store(
        string secretName,
        string value,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        try
        {
            await client.SetSecretAsync(secretName, value, cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == 403)
        {
            throw new SecretStoreUnavailableException(
                "This deployment's identity may read the vault but not write to it. Grant it the "
                    + "Key Vault Secrets Officer role, or name a secret that already exists."
            );
        }
    }
}
