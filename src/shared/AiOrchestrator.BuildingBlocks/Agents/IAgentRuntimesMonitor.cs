namespace AiOrchestrator.BuildingBlocks.Agents;

/// <summary>
/// What this process can say about the agent runtimes it would execute Runs with (#279). The
/// seam exists so the Runs module can render the panel without ever spawning a CLI: it reads a
/// snapshot in product vocabulary, and the habitat that actually executes keeps the snapshot
/// true.
/// <para>
/// A process that does not execute Runs itself answers <i>unhosted</i> rather than empty:
/// "these runtimes are not ready here" and "Runs execute somewhere this process cannot see"
/// are different sentences, and the panel must not render the second as the first.
/// </para>
/// </summary>
public interface IAgentRuntimesMonitor
{
    AgentRuntimesSnapshot Snapshot();
}

/// <summary>
/// One moment of the runtimes host, as the panel needs it (#279).
/// </summary>
/// <param name="Hosted">Runs execute in this process; everything else is meaningful only then.</param>
/// <param name="CheckedAt">When the last probe ran — the panel says "checked 20s ago".</param>
/// <param name="ProbeInterval">How long until the probe retries, so the panel can say so.</param>
/// <param name="Runtimes">Each registered runtime's readiness.</param>
public sealed record AgentRuntimesSnapshot(
    bool Hosted,
    DateTimeOffset? CheckedAt,
    TimeSpan ProbeInterval,
    IReadOnlyList<AgentRuntimeState> Runtimes
)
{
    /// <summary>The answer of a process that does not execute Runs itself.</summary>
    public static AgentRuntimesSnapshot Unhosted { get; } =
        new(Hosted: false, CheckedAt: null, ProbeInterval: TimeSpan.Zero, Runtimes: []);

    /// <summary>
    /// Which machine the runtimes below describe, and whether that machine is itself ready.
    /// Null where the question does not arise — an unhosted process, or one whose agents are
    /// its own children. Where agents run in sandboxes, "the CLI is ready" is meaningless until
    /// this says the sandbox host is (#279's promise, extended by the sandboxing change).
    /// </summary>
    public AgentHostState? Host { get; init; }
}

/// <summary>
/// The machine the agents run on, when that is not simply this process.
/// </summary>
/// <param name="Where">Named for a reader: "this process", "a per-Run sandbox on this machine".</param>
/// <param name="Ready">The host's own preconditions are met.</param>
/// <param name="Remedy">What to do when they are not — never a value, always an action.</param>
public sealed record AgentHostState(string Where, bool Ready, string? Remedy);

/// <summary>
/// One runtime's readiness. <paramref name="CredentialReady"/> is three-valued on purpose:
/// null means no credential is required (the switched-off state — the machine's own session),
/// which is a different sentence from "the credential resolves" and from "it does not".
/// </summary>
/// <param name="Name">The runtime name Automations select.</param>
/// <param name="Command">The executable the runtime spawns.</param>
/// <param name="CliReady">The executable answered the last probe.</param>
/// <param name="InstallCommand">The copyable remedy for a machine that lacks the executable.</param>
/// <param name="CredentialSecretName">The configured credential's name; null when none is required.</param>
/// <param name="CredentialReady">The credential resolved; null when none is required.</param>
public sealed record AgentRuntimeState(
    string Name,
    string Command,
    bool CliReady,
    string InstallCommand,
    string? CredentialSecretName,
    bool? CredentialReady
)
{
    /// <summary>
    /// Why this runtime's session could not be carried to the machine that runs it, when that is
    /// the reason it is not ready (#288). Null when the question does not arise.
    /// <para>
    /// Distinct from a missing secret on purpose: "the secret is not stored" and "you are signed
    /// in but your session lives somewhere a copy cannot reach" send a reader to different
    /// places, and only one of them is surprising on a machine you just logged into.
    /// </para>
    /// </summary>
    public string? SessionUnavailableReason { get; init; }

    /// <summary>
    /// The copyable command that starts the way out of <see cref="SessionUnavailableReason"/>.
    /// Null exactly when the reason is: they are one fact, said in two registers.
    /// </summary>
    public string? SessionUnavailableRemedy { get; init; }
}

/// <summary>
/// The default every habitat starts from; the host that executes Runs in-process replaces it.
/// Registered by the Runs module so the panel's endpoint always resolves, exactly like the
/// previews monitor: the ability is absent, never the answer.
/// </summary>
public sealed class UnhostedAgentRuntimesMonitor : IAgentRuntimesMonitor
{
    public AgentRuntimesSnapshot Snapshot() => AgentRuntimesSnapshot.Unhosted;
}
