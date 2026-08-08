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

    /// <summary>
    /// The habitat carries the machine owner's session into the sandbox, and this runtime's
    /// session is not somewhere a copy can reach (#288). It lives here, beside the others,
    /// because a Run that hits this and the panel that warns about it must say the same thing.
    /// <para>
    /// Deliberately not phrased as "secret missing": on a machine the developer is signed into,
    /// that reads as a bug in the product. The surprising fact is the one to lead with — the
    /// login exists, it just cannot be copied — and only then the two ways forward.
    /// </para>
    /// </summary>
    /// <param name="command">The CLI whose session cannot travel.</param>
    /// <param name="store">Where it keeps that session, named for a reader ("this machine's keychain").</param>
    /// <param name="runtimeName">The runtime, so the setting to point at the stored key can be named.</param>
    /// <param name="secretName">The name the key would be stored under — the copyable command uses the same one.</param>
    public static string SessionCannotTravel(
        string command,
        string store,
        string runtimeName,
        string secretName
    ) =>
        $"'{command}' keeps its session in {store}, not in a file, so the sandbox cannot be "
        + "given a copy of it. Store an API key in the sandbox host and point "
        + $"Agents:{runtimeName}:CredentialSecretName at '{secretName}', or run this Automation "
        + "on a runtime whose session travels.";

    /// <summary>
    /// The copyable half of <see cref="SessionCannotTravel"/> — the one command that starts the
    /// first way forward. The value stays in the host's own store; only the name is ever said
    /// (BR-010).
    /// </summary>
    public static string StoreSandboxSecret(string sandboxCommand, string secretName) =>
        $"{sandboxCommand} secret set -g {secretName}";
}
