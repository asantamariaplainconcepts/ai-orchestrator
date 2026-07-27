using AiOrchestrator.Modules.Backlog.Contracts;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog;

/// <summary>The Contracts read surface for conversations, implemented by the Connector's owner.</summary>
sealed class ConversationReader(ConnectorAccess access) : IConversationReader
{
    public async Task<ConversationResult> ReadSince(
        Guid projectId,
        string vendorStoryId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default
    )
    {
        var context = await access.Resolve(projectId, cancellationToken);
        if (context.IsError)
        {
            return new ConversationResult([], context.FirstError.Description);
        }

        var (connector, coordinates, token) = context.Value;

        var comments = await connector.ReadComments(
            coordinates,
            vendorStoryId,
            since,
            token,
            cancellationToken
        );

        return comments.IsError
            ? new ConversationResult([], comments.FirstError.Description)
            : new ConversationResult(
                [.. comments.Value.Select(c => new ConversationComment(c.Body, c.CreatedAt))],
                Failure: null
            );
    }
}
