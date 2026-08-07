namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// Where an agent CLI runs (design D1). Every runtime spawns its CLI through this one seam —
/// the reason <see cref="HeadlessProcess"/> existed as a single function — so BR-005's
/// kill-on-timeout and #96's streaming cannot drift between runtimes, and so a habitat can
/// change <b>where</b> the process runs without any runtime knowing.
/// <para>
/// Deliberately process-shaped: command, arguments, workspace, environment. Both implementations
/// today are processes (a child here, a command inside a sandbox), and the seam is internal to
/// composition — a future host with another shape adapts to this rather than the reverse.
/// </para>
/// </summary>
public interface IAgentProcessHost
{
    /// <summary>
    /// True when this host authenticates the agent's outbound requests itself, so credential
    /// values must NOT be handed to the process (design D2). False means values travel in the
    /// process's environment for its lifetime and nowhere else, as they always have (BR-010).
    /// </summary>
    bool SuppliesCredentials { get; }

    /// <summary>
    /// What a Run's transcript says the agent authenticated as — read whichever way
    /// <see cref="SuppliesCredentials"/> went, so the source is never left to inference.
    /// </summary>
    string CredentialSource { get; }

    Task<AgentProcessOutcome> Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? onOutput = null,
        /// <summary>
        /// Publish this for the life of the agent, where the host can (run-previews). A host with
        /// no sandbox has no port to publish and ignores it; the preview read then answers "not
        /// hosted here" rather than implying the Run failed to make one.
        /// </summary>
        BuildingBlocks.Agents.RunPreview? preview = null
    );

    /// <summary>
    /// Whether this host could run an agent at all, asked on the panel's cadence (design D6).
    /// The local host has nothing of its own to be missing; a sandbox host has a daemon, an
    /// identity and stored credentials, and each absence has its own remedy.
    /// </summary>
    Task<AgentHostReadiness> CheckReadiness(CancellationToken cancellationToken);

    /// <summary>
    /// Whether the runtime's CLI answers <b>where it will actually run</b> (design D6) — in this
    /// process for the local host, inside a sandbox for a sandbox host. Answering from this
    /// process's PATH when Runs execute elsewhere would state a truth no Run depends on.
    /// </summary>
    Task<bool> CliAnswers(string command, CancellationToken cancellationToken);
}

/// <summary>
/// What the host itself needs before any agent can run. <paramref name="Where"/> names the
/// machine for the panel, so "ready" is never ambiguous about which machine it describes.
/// </summary>
public sealed record AgentHostReadiness(bool Ready, string Where, string? Remedy)
{
    /// <summary>A host whose only precondition is the CLI the probe checks separately.</summary>
    public static readonly AgentHostReadiness Local = new(
        Ready: true,
        Where: "this process",
        Remedy: null
    );
}

/// <summary>
/// How the agent's process ended. <see cref="TimedOut"/> is BR-005's own outcome, distinct from
/// any exit code, because the reason a Run ends matters more than the number that carried it.
/// </summary>
public sealed record AgentProcessOutcome(bool TimedOut, int ExitCode, string Stdout, string Stderr);

/// <summary>
/// Raised when a host cannot start the agent at all — the boundary refused before any agent
/// code ran. Distinct from a non-zero exit, which is the agent's own verdict: this one carries a
/// remedy, because nothing retries (BR-004) and the person reading it must know what to fix.
/// </summary>
public sealed class AgentProcessHostException : Exception
{
    public AgentProcessHostException() { }

    public AgentProcessHostException(string message)
        : base(message) { }

    public AgentProcessHostException(string message, Exception innerException)
        : base(message, innerException) { }
}
