using AiOrchestrator.Modules.Backlog.Contracts;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog;

/// <summary>
/// The Contracts surface for a repository's open changes, implemented by the owner.
/// <para>
/// Listed against the same coordinates the Run executor publishes to — the Connector's own
/// repository — so a change the product opened is always in the population this reads. If
/// publishing ever moves to the configured code repository, this read moves with it.
/// </para>
/// </summary>
sealed class ChangeReader(ConnectorAccess access) : IChangeReader
{
    public async Task<OpenChanges> Open(
        Guid projectId,
        CancellationToken cancellationToken = default
    )
    {
        var context = await access.Resolve(projectId, cancellationToken);
        if (context.IsError)
        {
            // No Connector is a state — an Inbox group for a project that cannot have changes is
            // nothing, not an error line.
            return OpenChanges.None;
        }

        var (connector, coordinates, token) = context.Value;

        var changes = await connector.OpenChanges(coordinates, token, cancellationToken);

        return changes.IsError
            ? OpenChanges.Refused(changes.FirstError.Description)
            : new OpenChanges(
                [
                    .. changes.Value.Select(change => new OpenChangeView(
                        change.Number,
                        change.Title,
                        change.Url,
                        change.HeadBranch,
                        change.CreatedAt
                    )),
                ],
                Reason: null
            );
    }
}
