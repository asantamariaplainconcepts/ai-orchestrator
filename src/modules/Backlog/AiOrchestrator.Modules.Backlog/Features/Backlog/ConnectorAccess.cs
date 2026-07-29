using AiOrchestrator.BuildingBlocks.Secrets;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Domain;
using AiOrchestrator.Modules.Backlog.Persistence;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog;

/// <summary>
/// "Give me a usable Connector for this Project" — the sequence every vendor call needs:
/// find the stored Connector, pick the implementation for its vendor, resolve the credential
/// by name (BR-010). Extracted once three call sites wanted it; the refusals stay identical
/// because there is now one place that produces them.
/// </summary>
sealed class ConnectorAccess(
    BacklogDbContext database,
    IEnumerable<IBacklogConnector> connectors,
    ISecretResolver secrets
)
{
    public async Task<ErrorOr<ConnectorContext>> Resolve(
        Guid projectId,
        CancellationToken cancellationToken
    )
    {
        var connector = await database.Connectors.FirstOrDefaultAsync(
            entity => entity.ProjectId == projectId,
            cancellationToken
        );

        if (connector is null)
        {
            return BacklogErrors.ConnectorNotFound(projectId);
        }

        var implementation = connectors.FirstOrDefault(candidate =>
            candidate.Vendor == connector.Vendor
        );
        if (implementation is null)
        {
            return BacklogErrors.VendorUnavailable(
                $"no connector is registered for {connector.Vendor}"
            );
        }

        try
        {
            var token = await secrets.Resolve(connector.SecretName, cancellationToken);
            return new ConnectorContext(
                implementation,
                new BacklogCoordinates(connector.Owner, connector.Repository),
                token
            )
            {
                PromptDirectory = connector.PromptDirectory,
            };
        }
        catch (SecretNotFoundException)
        {
            return BacklogErrors.SecretNotFound(connector.SecretName);
        }
    }
}

/// <summary>The token is a value in memory for the length of one call — never stored (BR-010).</summary>
sealed record ConnectorContext(
    IBacklogConnector Connector,
    BacklogCoordinates Coordinates,
    string Token
)
{
    /// <summary>
    /// Where this project's prompts live, or null for the convention (#150). Deliberately not a
    /// positional parameter: three call sites deconstruct this record, and widening the deconstruction
    /// would have made every one of them mention a directory it has no use for.
    /// </summary>
    public string? PromptDirectory { get; init; }
}
