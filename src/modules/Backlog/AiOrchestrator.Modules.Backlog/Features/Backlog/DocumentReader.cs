using AiOrchestrator.Modules.Backlog.Contracts;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog;

/// <summary>The Contracts document read, implemented by the Connector's owner.</summary>
sealed class DocumentReader(ConnectorAccess access) : IDocumentReader
{
    public async Task<DocumentResult> Read(
        Guid projectId,
        string path,
        CancellationToken cancellationToken = default
    )
    {
        var context = await access.Resolve(projectId, cancellationToken);
        if (context.IsError)
        {
            return new DocumentResult(null, context.FirstError.Description);
        }

        var (connector, coordinates, token) = context.Value;

        // HEAD names the default branch on the vendors this product speaks, so no caller has to
        // know what a project calls its main line.
        var content = await connector.ReadDocument(
            coordinates,
            path,
            "HEAD",
            token,
            cancellationToken
        );

        return content.IsError
            ? new DocumentResult(null, content.FirstError.Description)
            : new DocumentResult(content.Value, Failure: null);
    }
}
