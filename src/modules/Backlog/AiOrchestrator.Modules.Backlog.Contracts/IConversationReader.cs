namespace AiOrchestrator.Modules.Backlog.Contracts;

/// <summary>
/// A Story's comments from a moment onwards, as other modules may read them. Live from the
/// vendor, never mirrored (BR-008): the mirror carries what triggers matching, and a
/// conversation is not that.
/// </summary>
public interface IConversationReader
{
    /// <summary>
    /// Oldest first. <see cref="ConversationResult.Failure"/> is null on success and otherwise a
    /// sentence — the caller decides whether a vendor hiccup ends a Run or merely delays a
    /// resume until the next check.
    /// </summary>
    Task<ConversationResult> ReadSince(
        Guid projectId,
        string vendorStoryId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default
    );
}

public sealed record ConversationResult(
    IReadOnlyList<ConversationComment> Comments,
    string? Failure
);

public sealed record ConversationComment(string Body, DateTimeOffset CreatedAt);
