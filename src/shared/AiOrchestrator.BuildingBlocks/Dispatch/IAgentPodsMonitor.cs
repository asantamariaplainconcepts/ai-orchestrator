namespace AiOrchestrator.BuildingBlocks.Dispatch;

/// <summary>
/// What this process can say about the Agent pods it hosts (design review 5b). The seam exists for
/// the same reason <see cref="IDispatchedRunHandler"/> does: the Runs module renders the panel
/// and must never know docker — it reads a snapshot in product vocabulary, and the habitat that
/// actually launches pods keeps the snapshot true.
/// <para>
/// A habitat that does not execute pods in this process answers <i>unhosted</i> rather than
/// empty: "there are no pods here" and "pods run somewhere this process cannot see" are
/// different sentences, and the panel must not render the second as the first.
/// </para>
/// </summary>
public interface IAgentPodsMonitor
{
    AgentPodsSnapshot Snapshot();
}

/// <summary>
/// One moment of the pod host, as the panel needs it. <paramref name="ImagePresent"/> is
/// three-valued on purpose: unknown while docker itself is unreachable — claiming the image is
/// missing when the daemon is down would send the operator to build instead of to start docker.
/// </summary>
/// <param name="Hosted">Pods execute in this process; everything else is meaningful only then.</param>
/// <param name="DockerReady">The docker daemon answered the last probe.</param>
/// <param name="ImagePresent">The pod image exists locally; null while docker is unreachable.</param>
/// <param name="CheckedAt">When the last probe ran — the panel says "checked 20s ago".</param>
/// <param name="ProbeInterval">How long until the probe retries, so the panel can say so.</param>
/// <param name="MaxConcurrentPods">The machine's slot count (design D6 of #246).</param>
/// <param name="Pods">Every Run currently inside the launcher, executing or waiting for a slot.</param>
public sealed record AgentPodsSnapshot(
    bool Hosted,
    bool DockerReady,
    bool? ImagePresent,
    DateTimeOffset? CheckedAt,
    TimeSpan ProbeInterval,
    int MaxConcurrentPods,
    IReadOnlyList<AgentPodSighting> Pods
)
{
    /// <summary>The answer of a process that does not host pods.</summary>
    public static AgentPodsSnapshot Unhosted { get; } =
        new(
            Hosted: false,
            DockerReady: false,
            ImagePresent: null,
            CheckedAt: null,
            ProbeInterval: TimeSpan.Zero,
            MaxConcurrentPods: 0,
            Pods: []
        );
}

/// <summary>
/// A Run as the launcher sees it: executing in a container, or holding the semaphore queue
/// waiting for a slot. <paramref name="SightedAt"/> is when the Run entered that phase, so the
/// panel can show how long a pod has been at work.
/// </summary>
public sealed record AgentPodSighting(Guid RunId, bool Executing, DateTimeOffset SightedAt);

/// <summary>
/// The default every habitat starts from; the host that composes a pod launcher replaces it.
/// Registered by the Runs module so the panel's endpoint always resolves, exactly like the
/// unavailable secret store: the ability is absent, never the answer.
/// </summary>
public sealed class UnhostedAgentPodsMonitor : IAgentPodsMonitor
{
    public AgentPodsSnapshot Snapshot() => AgentPodsSnapshot.Unhosted;
}
