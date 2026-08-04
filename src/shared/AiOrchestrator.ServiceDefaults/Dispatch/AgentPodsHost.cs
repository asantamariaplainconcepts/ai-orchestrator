using System.Collections.Concurrent;
using AiOrchestrator.BuildingBlocks.Dispatch;

namespace AiOrchestrator.ServiceDefaults.Dispatch;

/// <summary>
/// The pod host's ledger (design review 5b): which Runs the launcher currently holds and what
/// the last docker probe said. Today the only way to see a local pod is <c>docker ps</c> — this
/// is the in-process record the panel reads instead, kept by the two writers that already know
/// the facts: <see cref="PodRunLauncher"/> for sightings, <see cref="AgentPodsProbe"/> for
/// docker's health.
/// <para>
/// In-memory on purpose. The pod arrangement lives where the launcher lives (one process per
/// machine, #246), so this process's memory IS the machine's truth — a table would outlive the
/// pods it describes and lie after a restart.
/// </para>
/// </summary>
public sealed class AgentPodsHost(PodLaunchOptions options, TimeProvider time) : IAgentPodsMonitor
{
    /// <summary>
    /// One cadence for probing and for the panel's "retries every 30s" sentence — the copy reads
    /// the snapshot, so the promise and the behaviour cannot drift.
    /// </summary>
    public static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(30);

    readonly ConcurrentDictionary<Guid, AgentPodSighting> _pods = new();

    // One reference, swapped whole: a reader sees the previous probe or this one, never a mix of
    // both — the reason the three probe facts travel as a single record.
    volatile Probe? _probe;

    /// <summary>The Run entered the launcher and is waiting for one of the machine's slots.</summary>
    public void WaitingForSlot(Guid runId) =>
        _pods[runId] = new AgentPodSighting(runId, Executing: false, time.GetUtcNow());

    /// <summary>The Run took a slot; its container is starting.</summary>
    public void Executing(Guid runId) =>
        _pods[runId] = new AgentPodSighting(runId, Executing: true, time.GetUtcNow());

    /// <summary>The pod exited, however it went — the Run's own state carries the outcome.</summary>
    public void Finished(Guid runId) => _pods.TryRemove(runId, out _);

    public void RecordProbe(bool dockerReady, bool? imagePresent) =>
        _probe = new Probe(dockerReady, imagePresent, time.GetUtcNow());

    public AgentPodsSnapshot Snapshot()
    {
        var probe = _probe;
        return new AgentPodsSnapshot(
            Hosted: true,
            // Before the first probe lands this reads "not ready" with a null CheckedAt — the
            // honest sentence for a host still finding out, and the panel renders it as checking.
            DockerReady: probe?.DockerReady ?? false,
            ImagePresent: probe?.ImagePresent,
            CheckedAt: probe?.At,
            ProbeInterval: ProbeInterval,
            MaxConcurrentPods: options.MaxConcurrentPods,
            Pods:
            [
                .. _pods
                    .Values.OrderByDescending(pod => pod.Executing)
                    .ThenBy(pod => pod.SightedAt),
            ]
        );
    }

    sealed record Probe(bool DockerReady, bool? ImagePresent, DateTimeOffset At);
}
