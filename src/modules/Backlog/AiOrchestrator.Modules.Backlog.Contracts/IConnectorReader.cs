namespace AiOrchestrator.Modules.Backlog.Contracts;

/// <summary>
/// The Connector as other modules may know it: coordinates and the credential's <b>name</b>
/// (BR-010 — the value is resolved by whoever holds a vault identity, never read from here).
/// </summary>
public interface IConnectorReader
{
    Task<ConnectorSnapshot?> Find(Guid projectId, CancellationToken cancellationToken = default);
}

public sealed record ConnectorSnapshot(
    string Vendor,
    string Owner,
    string Repository,
    string SecretName
);
