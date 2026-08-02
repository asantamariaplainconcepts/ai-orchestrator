using AiOrchestrator.Modules.Backlog.Contracts;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog;

/// <summary>The Contracts document read, implemented by the Connector's owner.</summary>
sealed class DocumentReader(ConnectorAccess access) : IDocumentReader
{
    public Task<DocumentResult> Read(
        Guid projectId,
        string path,
        CancellationToken cancellationToken = default
    ) => Read(projectId, path, ResolvePath: null, cancellationToken);

    /// <summary>
    /// The prompt read: the name is resolved against the project's prompts directory here, where the
    /// Connector that holds it lives (design D6). The resolution runs after the Connector is known
    /// and before the vendor is called, so a refusal names the resolved path rather than the name the
    /// Admin typed.
    /// </summary>
    public Task<DocumentResult> ReadPrompt(
        Guid projectId,
        string name,
        CancellationToken cancellationToken = default
    ) =>
        Read(
            projectId,
            name,
            ResolvePath: (directory, requested) => PromptPath.Resolve(directory, requested),
            cancellationToken
        );

    async Task<DocumentResult> Read(
        Guid projectId,
        string path,
        Func<string?, string, (string? Path, string? Failure)>? ResolvePath,
        CancellationToken cancellationToken
    )
    {
        var context = await access.Resolve(projectId, cancellationToken);
        if (context.IsError)
        {
            return new DocumentResult(null, context.FirstError.Description);
        }

        var (connector, coordinates, token) = context.Value;

        if (ResolvePath is not null)
        {
            var (resolved, refusal) = ResolvePath(context.Value.PromptDirectory, path);
            if (refusal is not null)
            {
                return new DocumentResult(null, refusal);
            }

            path = resolved!;
        }

        var resolvedPath = ResolvePath is null ? null : path;

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
            ? new DocumentResult(null, content.FirstError.Description, resolvedPath)
            : new DocumentResult(content.Value, Failure: null, resolvedPath);
    }

    /// <summary>
    /// One candidate directory, read live (#229). Absence is an answer rather than a failure: the
    /// caller is probing several conventional locations, and treating "not there" as a refusal
    /// would make the common case look broken.
    /// </summary>
    public async Task<DirectoryListing> ListPromptFiles(
        Guid projectId,
        string directory,
        CancellationToken cancellationToken = default
    )
    {
        var context = await access.Resolve(projectId, cancellationToken);
        if (context.IsError)
        {
            return new DirectoryListing(
                directory,
                [],
                [],
                Absent: false,
                context.FirstError.Description
            );
        }

        var (connector, coordinates, token) = context.Value;

        var listing = await connector.ListDirectoryFiles(
            coordinates,
            directory,
            token,
            cancellationToken
        );

        if (listing.IsError)
        {
            return new DirectoryListing(
                directory,
                [],
                [],
                Absent: false,
                listing.FirstError.Description
            );
        }

        return listing.Value is null
            ? new DirectoryListing(directory, [], [], Absent: true, Failure: null)
            : new DirectoryListing(
                directory,
                [
                    .. listing.Value.Files.Where(name =>
                        name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                    ),
                ],
                listing.Value.Subdirectories,
                Absent: false,
                Failure: null
            );
    }
}
