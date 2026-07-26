using Microsoft.Extensions.Configuration;

namespace AiOrchestrator.BuildingBlocks.Secrets;

/// <summary>
/// Development resolver: reads secrets from configuration under the <c>Secrets:</c> section —
/// in practice .NET user-secrets or environment variables, neither of which is committed.
/// <para>
/// It reads on every call rather than caching, so it behaves like the Key Vault implementation
/// that replaces it in deployed environments: a value changed while the app runs is picked up.
/// Configuration providers that support reload make that literally true; the contract is the same
/// either way, which is what keeps the two implementations interchangeable.
/// </para>
/// </summary>
public sealed class ConfigurationSecretResolver(IConfiguration configuration) : ISecretResolver
{
    public const string SectionName = "Secrets";

    public Task<string> Resolve(string secretName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        var value = configuration[$"{SectionName}:{secretName}"];

        return string.IsNullOrEmpty(value)
            ? throw new SecretNotFoundException(secretName)
            : Task.FromResult(value);
    }
}
