namespace AiOrchestrator.BuildingBlocks.Agents;

/// <summary>
/// The remedy sentences, spelled once per cause (#279 design D3): the probe's verdicts, the
/// panel's read and a Run's failure reason all say the same thing, so they cannot drift. Names
/// always, values never (BR-010).
/// </summary>
public static class AgentRuntimeRemedies
{
    /// <summary>
    /// The pinned install commands. The same versions the conversation session's image bakes
    /// (src/root/AiOrchestrator.ConversationSession/Dockerfile) — a Run and a conversation that
    /// answered from different CLI versions would disagree about the same repository.
    /// </summary>
    public const string InstallOpenCode = "npm install -g opencode-ai@1.18.6";

    public const string InstallClaudeCode = "npm install -g @anthropic-ai/claude-code@2.0.44";

    /// <summary>The executable was not found where this process runs.</summary>
    public static string MissingCli(string command, string installCommand) =>
        $"The agent CLI '{command}' is not on this process's PATH, so the Run could not start. "
        + $"Install it ({installCommand}) and run again — the server does not need a restart.";

    /// <summary>
    /// The AI credential's name resolves to nothing. Distinct from the vendor credential's
    /// failure: only the AI credential has the switched-off alternative.
    /// </summary>
    public static string MissingAiCredential(string secretName, string runtimeName) =>
        $"No secret named '{secretName}' was found. Add it to the configured secret store, or "
        + $"clear Agents:{runtimeName}:CredentialSecretName to run with this machine's own "
        + "session instead.";
}
