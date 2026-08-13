using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Backlog.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog;

/// <summary>The Contracts read surface for the Connector — names and coordinates, never values.</summary>
sealed class ConnectorReader(BacklogDbContext database) : IConnectorReader
{
    public async Task<ConnectorSnapshot?> Find(
        Guid projectId,
        CancellationToken cancellationToken = default
    )
    {
        var connector = await database.Connectors.FirstOrDefaultAsync(
            entity => entity.ProjectId == projectId,
            cancellationToken
        );

        return connector is null
            ? null
            : new ConnectorSnapshot(
                connector.Vendor.ToString(),
                connector.Owner,
                connector.Repository,
                // The Connector's own answer to "which source?", so a consumer resolves through the
                // one seam rather than reconstructing a reference from a name that may not exist
                // (DEC-069). Null is a corrupt row — see Connector.Credential.
                connector.Credential(),
                connector.CodeSource.ToString(),
                connector.LocalPath
            )
            {
                PromptDirectory = connector.PromptDirectory,
                LocalSetupCommand = connector.LocalSetupCommand,
            };
    }
}
