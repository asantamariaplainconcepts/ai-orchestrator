using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiOrchestrator.ServiceDefaults.Identity;

/// <summary>
/// Where the Data Protection key ring lives (#180).
/// <para>
/// Unpersisted, the ring is generated in memory per process — and the OIDC <c>state</c> is
/// encrypted by whichever instance issued the challenge and decrypted by whichever handles the
/// callback. With <c>min_replicas = 0</c> and a new revision per deploy, those are routinely not the
/// same process, so sign-in failed with <i>"Unable to unprotect the message.State"</i> on every real
/// attempt. Nothing about the provider, the cookies or the redirect URIs was wrong; the keys simply
/// did not survive.
/// </para>
/// <para>
/// Composed on configuration presence, like every other habitat decision here: a blob URI means
/// persist there, its absence means keep the in-memory ring — which is correct for a machine one
/// person owns, where one process issues and handles everything.
/// </para>
/// <para>
/// The ring is wrapped with a Key Vault key rather than written in the clear. This is
/// authentication material: an unwrapped ring readable from blob storage is forgeable session
/// cookies for anyone who can read that blob, which is a different and much worse thing than a
/// leaked cache.
/// </para>
/// </summary>
public static class KeyRingPersistence
{
    /// <summary>Blob holding the ring — full URI including container and blob name.</summary>
    public const string BlobUriKey = "DataProtection:KeyRingBlobUri";

    /// <summary>Key Vault key that wraps it — full key identifier.</summary>
    public const string WrappingKeyIdKey = "DataProtection:WrappingKeyId";

    public static TBuilder AddPersistedKeyRing<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var blobUri = builder.Configuration[BlobUriKey];
        if (string.IsNullOrWhiteSpace(blobUri))
        {
            return builder;
        }

        // The same credential the vault and the queue already use: the workload identity, told
        // which one it is by AZURE_CLIENT_ID because DefaultAzureCredential will not guess when a
        // deployment offers more than one.
        var credential = new DefaultAzureCredential();

        var protection = builder
            .Services.AddDataProtection()
            // Explicit, because the default is derived from the entry assembly name and a rename
            // would silently invalidate every existing session and every in-flight sign-in.
            .SetApplicationName("ai-orchestrator")
            .PersistKeysToAzureBlobStorage(new Uri(blobUri), credential);

        var wrappingKeyId = builder.Configuration[WrappingKeyIdKey];
        if (!string.IsNullOrWhiteSpace(wrappingKeyId))
        {
            protection.ProtectKeysWithAzureKeyVault(new Uri(wrappingKeyId), credential);
        }

        return builder;
    }
}
