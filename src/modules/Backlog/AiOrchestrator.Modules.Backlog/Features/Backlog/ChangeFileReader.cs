using AiOrchestrator.Modules.Backlog.Contracts;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog;

/// <summary>The Contracts surface for a Story's change files, implemented by the owner.</summary>
sealed class ChangeFileReader(ConnectorAccess access) : IChangeFileReader
{
    public async Task<ChangeFiles?> ForStory(
        Guid projectId,
        string vendorStoryId,
        CancellationToken cancellationToken = default
    )
    {
        var context = await access.Resolve(projectId, cancellationToken);
        if (context.IsError)
        {
            return null;
        }

        var (connector, coordinates, token) = context.Value;

        var change = await connector.FindLinkedChange(
            coordinates,
            vendorStoryId,
            token,
            cancellationToken
        );
        if (change.IsError || change.Value is null)
        {
            return null;
        }

        var files = await connector.ListChangeFiles(
            coordinates,
            change.Value.Number,
            token,
            cancellationToken
        );

        return files.IsError
            ? null
            : new ChangeFiles(
                change.Value.Number,
                change.Value.Url,
                [
                    .. files.Value.Select(file => new ChangedFileView(
                        file.Path,
                        file.Status,
                        file.Additions,
                        file.Deletions,
                        file.Patch,
                        file.PatchOmitted?.ToString()
                    )),
                ]
            );
    }
}
