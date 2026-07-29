using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

namespace AiOrchestrator.BuildingBlocks.Secrets;

/// <summary>
/// The self-host habitat's store (#124, design D4 as revised at implementation): one file per
/// secret, its contents produced by ASP.NET Core Data Protection, in a directory the deployment
/// mounts.
/// <para>
/// The framework's implementation or nothing. Every decision a bespoke design would have to make
/// here — cipher, key derivation, IV handling, envelope format, key rotation — has a well-known
/// wrong answer that looks correct in a passing test. Data Protection exists so applications stop
/// making them.
/// </para>
/// <para>
/// Files rather than a table, which is a change from the proposal's design D4 and a strictly
/// stronger property: a leaked database dump contains no credential at all, because none is in
/// the database. It also spares this seam a DbContext, a schema and a migration that would have
/// had to live outside every module — the modules own their schemas, and secrets belong to no
/// module.
/// </para>
/// </summary>
public sealed class ProtectedFileSecretStore : ISecretStore
{
    /// <summary>Where protected values live. Its presence is what selects this store.</summary>
    public const string DirectoryKey = "Secrets:LocalStorePath";

    /// <summary>
    /// Where Data Protection persists its key ring. Separate from the values on purpose: one
    /// directory holding both is obfuscation, not encryption.
    /// </summary>
    public const string KeyRingKey = "Secrets:LocalKeyRingPath";

    readonly IDataProtector _protector;
    readonly string _directory;

    public ProtectedFileSecretStore(IDataProtector protector, string directory)
    {
        _protector = protector;
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public async Task Store(
        string secretName,
        string value,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var path = PathFor(_directory, secretName);

        // Written beside the target and moved into place: a crash mid-write must not leave a
        // half-file that decrypts to nothing, because the failure would read as "credential
        // rejected" and send the next reader to GitHub rather than here.
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporary, _protector.Protect(value), cancellationToken);
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>
    /// A file name that is stable for a name, unique across names, and safe on every filesystem.
    /// Hashed rather than escaped: escaping has to be reversible and therefore has edge cases,
    /// and nothing here ever needs to read the name back out of the path.
    /// </summary>
    internal static string PathFor(string directory, string secretName)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(secretName));
        return Path.Combine(directory, $"{Convert.ToHexStringLower(digest)}.secret");
    }
}

/// <summary>
/// Reads what <see cref="ProtectedFileSecretStore"/> wrote, falling back to configuration.
/// <para>
/// The fallback is the whole point of composing rather than replacing: a self-hoster who already
/// set <c>Secrets__&lt;name&gt;</c> in their environment keeps working exactly as before, and a
/// value pasted through the portal is found first. Neither habitat has to be migrated into the
/// other.
/// </para>
/// </summary>
public sealed class ProtectedFileSecretResolver(
    IDataProtector protector,
    string directory,
    IConfiguration configuration
) : ISecretResolver
{
    readonly ConfigurationSecretResolver _fallback = new(configuration);

    public async Task<string> Resolve(
        string secretName,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        var path = ProtectedFileSecretStore.PathFor(directory, secretName);
        if (!File.Exists(path))
        {
            return await _fallback.Resolve(secretName, cancellationToken);
        }

        var protectedValue = await File.ReadAllTextAsync(path, cancellationToken);
        try
        {
            var value = protector.Unprotect(protectedValue);

            // An empty stored value is a misconfiguration wearing the costume of a working one.
            return string.IsNullOrEmpty(value)
                ? throw new SecretNotFoundException(secretName)
                : value;
        }
        catch (CryptographicException exception)
        {
            // The key ring that produced this file is gone or has been replaced. Saying so beats
            // "not found", which would send the reader looking for a secret that is right there.
            throw new SecretNotFoundException(
                $"The stored value for '{secretName}' cannot be decrypted with this deployment's "
                    + "key ring. Store the secret again.",
                exception
            );
        }
    }
}
