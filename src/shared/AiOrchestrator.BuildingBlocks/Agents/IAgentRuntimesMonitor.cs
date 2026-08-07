namespace AiOrchestrator.BuildingBlocks.Agents;

/// <summary>
/// What this process can say about the agent runtimes it would execute Runs with (#279). The
/// seam exists for the same reason <see cref="Dispatch.IAgentPodsMonitor"/> does: the Runs
/// module renders the panel and must never spawn CLIs — it reads a snapshot in product
/// vocabulary, and the habitat that actually executes keeps the snapshot true.
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
/// One moment of the runtimes host, as the panel needs it (#279, mirroring
/// <see cref="Dispatch.AgentPodsSnapshot"/>).
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
}

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
);

/// <summary>
/// The default every habitat starts from; the host that executes Runs in-process replaces it.
/// Registered by the Runs module so the panel's endpoint always resolves, exactly like the
/// pods monitor: the ability is absent, never the answer.
/// </summary>
public sealed class UnhostedAgentRuntimesMonitor : IAgentRuntimesMonitor
{
    public AgentRuntimesSnapshot Snapshot() => AgentRuntimesSnapshot.Unhosted;
}
