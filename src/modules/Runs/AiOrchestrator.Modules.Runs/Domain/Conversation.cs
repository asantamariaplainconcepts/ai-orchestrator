using AiOrchestrator.BuildingBlocks.Domain;

namespace AiOrchestrator.Modules.Runs.Domain;

/// <summary>
/// A person talking to an agent about a project, or about one of its Stories (#166, ADR-0008).
/// <para>
/// <b>Deliberately not a Run.</b> It occupies no cap slot, holds no lock on any Story and blocks
/// nothing — which is what keeps BR-001 and BR-014 untouched. Waiting blocks a Story precisely
/// because a Run occupies it, and a conversation that occupied one would stop every Automation on
/// that Story for as long as somebody kept talking, with BR-006 putting no limit on how long that
/// is.
/// </para>
/// <para>
/// The consequence, stated rather than discovered: BR-002's cap counts Runs, so nothing here caps
/// concurrent conversations. Making them count is a rule change with its own decision (#166's known
/// risk), and this type does not invent one.
/// </para>
/// </summary>
sealed class Conversation : Aggregate
{
    Conversation() { }

    Conversation(Guid projectId, string? vendorStoryId, DateTimeOffset startedAt)
    {
        ProjectId = projectId;
        VendorStoryId = vendorStoryId;
        StartedAt = startedAt;
        LastActivityAt = startedAt;
    }

    public Guid ProjectId { get; private set; }

    /// <summary>
    /// The Story this is about, or null. Null is an ordinary case and not a degraded one: "what
    /// would you do here" is a question about a project, and forcing a subject would make the
    /// commonest question unaskable.
    /// </summary>
    public string? VendorStoryId { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>
    /// When it was last used. The session host reclaims its container on inactivity, so this is what
    /// tells a reader whether the next message will be instant or pay a start.
    /// </summary>
    public DateTimeOffset LastActivityAt { get; private set; }

    readonly List<ConversationMessage> _messages = [];

    public IReadOnlyList<ConversationMessage> Messages => _messages;

    public static Conversation Start(
        Guid projectId,
        string? vendorStoryId,
        DateTimeOffset startedAt
    ) => new(projectId, vendorStoryId, startedAt);

    /// <summary>What the person said. Recorded before the pass runs, so a crash leaves the question.</summary>
    public ConversationMessage Ask(string body, DateTimeOffset at)
    {
        var message = ConversationMessage.FromPerson(Id, body, at);
        _messages.Add(message);
        LastActivityAt = at;
        return message;
    }

    /// <summary>What the agent said, and what the pass cost.</summary>
    public ConversationMessage Answer(
        string body,
        DateTimeOffset at,
        long? inputTokens,
        long? outputTokens,
        decimal? costUsd
    )
    {
        var message = ConversationMessage.FromAgent(
            Id,
            body,
            at,
            inputTokens,
            outputTokens,
            costUsd
        );
        _messages.Add(message);
        LastActivityAt = at;
        return message;
    }

    /// <summary>
    /// A pass that failed. A message, not an ending: the conversation stays open and accepts another,
    /// because a model that timed out once says nothing about the next question.
    /// </summary>
    public ConversationMessage Fail(string reason, DateTimeOffset at)
    {
        var message = ConversationMessage.Failure(Id, reason, at);
        _messages.Add(message);
        LastActivityAt = at;
        return message;
    }

    /// <summary>
    /// What has been spent, and whether that is the whole story. BR-011: a pass whose usage the
    /// runtime did not report reads unknown rather than zero, so a total that includes one is a
    /// floor and must not be presented as exact (design D4).
    /// </summary>
    public (decimal Known, bool Complete) Spend()
    {
        var agentMessages = _messages.Where(message => message.Role == ConversationRole.Agent);

        return (
            agentMessages.Sum(message => message.CostUsd ?? 0m),
            agentMessages.All(message => message.CostUsd is not null)
        );
    }
}

/// <summary>One turn. Its usage is the pass's, and absent usage stays absent (BR-011).</summary>
sealed class ConversationMessage : BaseEntity
{
    ConversationMessage() { }

    ConversationMessage(Guid conversationId, ConversationRole role, string body, DateTimeOffset at)
    {
        ConversationId = conversationId;
        Role = role;
        Body = body;
        CreatedAt = at;
    }

    public Guid ConversationId { get; private set; }

    public ConversationRole Role { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public long? InputTokens { get; private set; }

    public long? OutputTokens { get; private set; }

    /// <summary>Null means the runtime reported nothing — unknown, never zero (BR-011).</summary>
    public decimal? CostUsd { get; private set; }

    /// <summary>Set on a failed pass; the conversation is still open.</summary>
    public bool Failed { get; private set; }

    public static ConversationMessage FromPerson(
        Guid conversationId,
        string body,
        DateTimeOffset at
    ) => new(conversationId, ConversationRole.Person, body, at);

    public static ConversationMessage FromAgent(
        Guid conversationId,
        string body,
        DateTimeOffset at,
        long? inputTokens,
        long? outputTokens,
        decimal? costUsd
    ) =>
        new(conversationId, ConversationRole.Agent, body, at)
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CostUsd = costUsd,
        };

    public static ConversationMessage Failure(
        Guid conversationId,
        string reason,
        DateTimeOffset at
    ) => new(conversationId, ConversationRole.Agent, reason, at) { Failed = true };
}

enum ConversationRole
{
    Person = 1,
    Agent = 2,
}
