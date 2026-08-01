namespace AiOrchestrator.Modules.Backlog.Contracts;

/// <summary>
/// The Connector as other modules may know it: coordinates and the credential's <b>name</b>
/// (BR-010 — the value is resolved by whoever holds a vault identity, never read from here).
/// </summary>
public interface IConnectorReader
{
    Task<ConnectorSnapshot?> Find(Guid projectId, CancellationToken cancellationToken = default);
}

/// <summary>
/// <paramref name="CodeSource"/> is the enum's name (<c>Repository</c> | <c>LocalFolder</c>) and
/// <paramref name="LocalPath"/> travels only with the latter (#210) — the Runs module derives a
/// Run's default execution locus from it and the executor picks the workspace by it.
/// </summary>
public sealed record ConnectorSnapshot(
    string Vendor,
    string Owner,
    string Repository,
    string SecretName,
    string CodeSource,
    string? LocalPath
);
