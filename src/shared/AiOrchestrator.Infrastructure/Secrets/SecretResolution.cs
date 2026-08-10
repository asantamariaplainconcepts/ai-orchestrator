using AiOrchestrator.BuildingBlocks.Secrets;
using Aspire.Azure.Security.KeyVault;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiOrchestrator.ServiceDefaults.Secrets;

/// <summary>
/// Composes secret resolution for a host, choosing by configuration rather than by environment
/// name — the lesson of the migration bootstrap, where a gate on <c>IsProduction()</c> guessed
/// wrong and skipped silently. A vault URI is present or it is not; there is nothing to infer.
/// <para>
/// This lives in ServiceDefaults rather than in each host so the Server and the MigrationService
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
            AddUnvaultedSecrets(builder);
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

        // Storing is composed here, beside resolving, for the same reason and in the same place:
        // a module structurally cannot call an IHostApplicationBuilder extension (design D3), so
        // this is the only spot that can know which store the habitat has (#124, design D1).
        builder.Services.AddSingleton<ISecretStore, KeyVaultSecretStore>();

        ResolveConnectionStrings(builder);
        return builder;
    }

    /// <summary>
    /// No vault: either the deployment mounts a directory for protected values (the self-host
    /// habitat), or values arrive as configuration the product does not own (development and
    /// tests) and storing is refused with the remedy in the refusal.
    /// </summary>
    static void AddUnvaultedSecrets(IHostApplicationBuilder builder)
    {
        var directory = builder.Configuration[ProtectedFileSecretStore.DirectoryKey];

        if (string.IsNullOrWhiteSpace(directory))
        {
            builder.Services.AddSingleton<ISecretResolver, ConfigurationSecretResolver>();
            builder.Services.AddSingleton<ISecretStore>(
                new UnavailableSecretStore(
                    $"Set {ProtectedFileSecretStore.DirectoryKey} to store values here, or add "
                        + $"{ConfigurationSecretResolver.SectionName}__<name> to the environment "
                        + "and configure the Connector with that name."
                )
            );
            return;
        }

        // The key ring lives apart from the values on purpose: one directory holding both is
        // obfuscation, not encryption (design D4). Defaulting it beside the store would quietly
        // undo that, so an unset key-ring path is a refusal to start, not a default.
        var keyRing = builder.Configuration[ProtectedFileSecretStore.KeyRingKey];
        if (string.IsNullOrWhiteSpace(keyRing))
        {
            throw new InvalidOperationException(
                $"{ProtectedFileSecretStore.DirectoryKey} is set without "
                    + $"{ProtectedFileSecretStore.KeyRingKey}. Protected values and the key that "
                    + "protects them must be persisted separately, or a single stolen volume "
                    + "yields both. Mount a second location and name it."
            );
        }

        builder
            .Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyRing))
            .SetApplicationName(DataProtectionApplicationName);

        builder.Services.AddSingleton<ISecretResolver>(provider => new ProtectedFileSecretResolver(
            Protector(provider),
            directory,
            provider.GetRequiredService<IConfiguration>()
        ));

        builder.Services.AddSingleton<ISecretStore>(provider => new ProtectedFileSecretStore(
            Protector(provider),
            directory
        ));
    }

    /// <summary>
    /// Fixed, because Data Protection derives keys from it: letting it default to the entry
    /// assembly's name would make the Server unable to read what the dispatch worker stored,
    /// and the failure would look like a rejected credential rather than a naming mismatch.
    /// </summary>
    const string DataProtectionApplicationName = "ai-orchestrator";

    const string ProtectorPurpose = "AiOrchestrator.Secrets.v1";

    static IDataProtector Protector(IServiceProvider provider) =>
        provider.GetRequiredService<IDataProtectionProvider>().CreateProtector(ProtectorPurpose);

    /// <summary>
    /// Turns <c>ConnectionStrings:&lt;name&gt;SecretName</c> into <c>ConnectionStrings:&lt;name&gt;</c>
    /// by reading the vault at startup.
    /// <para>
    /// This bridge exists because the two halves genuinely disagree about timing: BR-010 resolves
    /// secrets <i>per use</i>, but EF Core reads its connection string once, when the DbContext is
    /// registered. The first deployed migration run failed on exactly that gap — the module asked
    /// configuration for a connection string that only existed as a secret name.
    /// </para>
    /// <para>
    /// Doing it in the composition root keeps modules unchanged and unaware, which is the point of
    /// design D3. Startup-time is also honest rather than a compromise: a rotated database password
    /// requires a restart regardless, because pooled connections outlive any per-call resolution.
    /// Blocking here is deliberate — an application that cannot reach its database should fail at
    /// startup, loudly, not on its first request.
    /// </para>
    /// </summary>
    static void ResolveConnectionStrings(IHostApplicationBuilder builder)
    {
        const string suffix = "SecretName";

        var client = new SecretClient(
            new Uri(builder.Configuration[KeyVaultUriKey]!),
            new DefaultAzureCredential()
        );

        var resolved = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var entry in builder.Configuration.GetSection("ConnectionStrings").GetChildren())
        {
            if (
                !entry.Key.EndsWith(suffix, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(entry.Value)
            )
            {
                continue;
            }

            var connectionName = entry.Key[..^suffix.Length];
            resolved[$"ConnectionStrings:{connectionName}"] = client
                .GetSecret(entry.Value)
                .Value.Value;
        }

        if (resolved.Count > 0)
        {
            builder.Configuration.AddInMemoryCollection(resolved);
        }
    }
}
