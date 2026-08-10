using System.Collections.Concurrent;
using AiOrchestrator.BuildingBlocks.Agents;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// The previews this process is currently serving (run-previews design D2): one entry per
/// executing Run whose sandbox published a port, written when the sandbox is created and removed
/// when it is disposed.
/// <para>
/// In memory on purpose: a stored row would outlive the
/// sandbox it describes and lie after a restart. A preview has exactly that property — it exists
/// while its sandbox does and not one moment longer, so the record must die with the process
/// that holds them.
/// </para>
/// <para>
/// There is deliberately no way to add an entry except beside creating a sandbox, and no way to
/// remove one except beside disposing it. That is what makes "a finished Run offers nothing" a
/// property rather than a branch somebody has to remember to write.
/// </para>
/// </summary>
public sealed class RunPreviewHost : IRunPreviewMonitor
{
    readonly ConcurrentDictionary<Guid, int> _ports = new();

    public bool Hosted => true;

    public int? PortFor(Guid runId) => _ports.TryGetValue(runId, out var port) ? port : null;

    /// <summary>The sandbox published a port for this Run; it is reachable until disposal.</summary>
    public void Published(Guid runId, int hostPort) => _ports[runId] = hostPort;

    /// <summary>The sandbox is gone, so the preview is. Idempotent — disposal can race a crash.</summary>
    public void Gone(Guid runId) => _ports.TryRemove(runId, out _);
}
