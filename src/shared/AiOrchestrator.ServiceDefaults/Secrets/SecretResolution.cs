using AiOrchestrator.BuildingBlocks.Secrets;
using Aspire.Azure.Security.KeyVault;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiOrchestrator.ServiceDefaults.Secrets;

/// <summary>
/// Composes secret resolution for a host, choosing by configuration rather than by environment
/// name — the lesson of the migration bootstrap, where a gate on <c>IsProduction()</c> guessed
/// wrong and skipped silently. A vault URI is present or it is not; there is nothing to infer.
/// <para>
/// This lives in BuildingBlocks rather than in each host so the Server and the MigrationService
/// cannot drift apart on how they reach secrets. It is an <c>IHostApplicationBuilder</c>
/// extension, which is precisely what a module structurally cannot call (design D3) — that is
/// what keeps modules free of any cloud SDK.
/// </para>
/// </summary>
public static class SecretResolution
{
    /// <summary>Configuration key whose presence selects Key Vault over configuration.</summary>
    public const string KeyVaultUriKey = "Secrets:KeyVaultUri";

    public static TBuilder AddSecretResolution<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var vaultUri = builder.Configuration[KeyVaultUriKey];

        if (string.IsNullOrWhiteSpace(vaultUri))
        {
            builder.Services.AddSingleton<ISecretResolver, ConfigurationSecretResolver>();
            return builder;
        }

        // Aspire's integration supplies the SecretClient with DefaultAzureCredential (the
        // container app's managed identity when deployed, the developer's az login locally),
        // plus retries, health checks and telemetry we would otherwise hand-roll.
        builder.AddAzureKeyVaultClient(
            "secrets",
            settings => settings.VaultUri = new Uri(vaultUri)
        );

        builder.Services.AddSingleton<ISecretResolver, KeyVaultSecretResolver>();
        return builder;
    }
}
