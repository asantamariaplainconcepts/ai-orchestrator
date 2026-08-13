namespace AiOrchestrator.BuildingBlocks.Agents;

/// <summary>
/// Where a conversation's agent pass runs (#166, design D3).
/// <para>
/// A seam rather than a call to the session host, for the reason every seam here exists: the module
/// that owns conversations must not know whether the habitat has an on-demand session host, a
/// process, or something later. Composed on the presence of the host's configuration and never
/// inferred from anything else (ADR-0010).
/// </para>
/// <para>
/// One call is one pass, which is ADR-0008's whole model. Nothing here batches, retries or resumes:
/// a failed pass is an answer the caller records as a failure, and the conversation stays open.
/// </para>
/// </summary>
public interface IConversationRuntime
{
    /// <summary>
    /// Answers one message. <paramref name="conversationId"/> is what a host that keeps a container
    /// per conversation addresses it by — the same conversation reaches the same warm workspace, and
    /// a host without one simply ignores it.
    /// </summary>
    Task<ConversationReply> Answer(
        Guid conversationId,
        ConversationContext context,
        string message,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// What the agent is given. The repository is named rather than cloned here: cloning is the host's,
/// because the credential belongs on the far side of the seam and never in the portal (DEC-030).
/// </summary>
/// <param name="ProjectId">Whose credential and whose repository.</param>
/// <param name="SecretName">The credential's <b>name</b>, never its value (BR-010).</param>
/// <param name="Code">Where the code is, in the same vendor-neutral terms a Run's workspace uses.</param>
/// <param name="StoryContext">
/// The Story this conversation is about, read from the mirror (BR-008), or null when it is about the
/// project only — which is an ordinary case, not a missing one.
/// </param>
public sealed record ConversationContext(
    Guid ProjectId,
    /// <summary>
    /// Null where the Connector authenticates as its host (DEC-069): there is no secret to name,
    /// and the agent reaches the vendor as the machine's own tooling already does. A deployed
    /// conversation can never be on that path, because the host path is self-host only.
    /// </summary>
    string? SecretName,
    CodeCoordinates Code,
    string? StoryContext
);

/// <summary>
/// What came back. <paramref name="Usage"/> is null when the runtime reported none — unknown, never
/// zero (BR-011), and the caller is what keeps that distinction visible.
/// </summary>
public sealed record ConversationReply(bool Succeeded, string Body, AgentUsage? Usage);
