using AiOrchestrator.BuildingBlocks.Agents;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// The runtimes ledger of a process that executes Runs itself (#279): the probe writes each
/// sweep's verdicts, the panel's endpoint reads the snapshot — the same division of labour as
/// <see cref="Dispatch.AgentPodsHost"/>, one seam over.
/// </summary>
public sealed class AgentRuntimesHost(TimeProvider time) : IAgentRuntimesMonitor
{
    /// <summary>
    /// One cadence for probing and for the panel's "retries every 30s" sentence — the copy
    /// reads the snapshot, so the promise and the behaviour cannot drift. The pods probe's
    /// constant, restated here because the two probes are siblings, not one machine.
    /// </summary>
    public static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(30);

    readonly Lock _gate = new();
    IReadOnlyList<AgentRuntimeState> _runtimes = [];
    DateTimeOffset? _checkedAt;

    public void RecordProbe(IReadOnlyList<AgentRuntimeState> runtimes)
    {
        lock (_gate)
        {
            _runtimes = runtimes;
            _checkedAt = time.GetUtcNow();
        }
    }

    public AgentRuntimesSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new AgentRuntimesSnapshot(
                Hosted: true,
                CheckedAt: _checkedAt,
                ProbeInterval: ProbeInterval,
                Runtimes: _runtimes
            );
        }
    }
}
