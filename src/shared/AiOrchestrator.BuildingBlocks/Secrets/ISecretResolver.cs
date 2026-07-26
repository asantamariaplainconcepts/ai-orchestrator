namespace AiOrchestrator.BuildingBlocks.Secrets;

/// <summary>
/// Resolves a secret value from its name, <b>per read</b>.
/// <para>
/// Per read is the whole point: Connector secrets are created while the application is running,
/// so anything that snapshots the set of secrets at startup cannot see one an Admin adds
/// afterwards — and the failure would read as "credential missing" rather than "process is
/// stale". It also means a rotated value is picked up with no restart and no cache to invalidate.
/// </para>
/// <para>
/// Implementations are registered by the <b>host</b>, never by a module: modules receive only
/// <c>IServiceCollection</c>/<c>IConfiguration</c> and must stay free of any secret-store SDK.
/// </para>
/// </summary>
public interface ISecretResolver
{
    /// <summary>
    /// The value stored under <paramref name="secretName"/>.
    /// Throws <see cref="SecretNotFoundException"/> when it is absent — never returns null,
    /// empty, or a default, because a silently empty credential fails far from its cause.
    /// </summary>
    Task<string> Resolve(string secretName, CancellationToken cancellationToken = default);
}

/// <summary>Thrown when a named secret does not exist. Carries the name so the fix is obvious.</summary>
public sealed class SecretNotFoundException : Exception
{
    public SecretNotFoundException(string secretName)
        : base(
            $"No secret named '{secretName}' was found. Add it to the configured secret store."
        ) => SecretName = secretName;

    public SecretNotFoundException() { }

    public SecretNotFoundException(string message, Exception innerException)
        : base(message, innerException) { }

    public string? SecretName { get; }
}
