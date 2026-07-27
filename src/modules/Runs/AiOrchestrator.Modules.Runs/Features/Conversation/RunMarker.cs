namespace AiOrchestrator.Modules.Runs.Features.Conversation;

/// <summary>
/// The signature the agent leaves on its own comments. One project PAT (DEC-030) means the
/// agent's comments and a human's can come from the same vendor account, so authorship cannot
/// distinguish a question from its answer — this can, whoever posted it (design D2).
/// <para>
/// An HTML comment renders invisibly on every vendor this product speaks to, so the Story reads
/// as a conversation, not as machine traffic.
/// </para>
/// </summary>
static class RunMarker
{
    const string Prefix = "<!-- aio:run:";

    const string Suffix = " -->";

    public static string For(Guid runId) => $"{Prefix}{runId:D}{Suffix}";

    public static string Sign(Guid runId, string body) => $"{For(runId)}\n{body}";

    /// <summary>
    /// Any run's marker, not a specific one: a comment from run A must not resume run B, but
    /// neither must it count as B's answer.
    /// </summary>
    public static bool IsAgentComment(string body) =>
        body.TrimStart().StartsWith(Prefix, StringComparison.Ordinal);
}
