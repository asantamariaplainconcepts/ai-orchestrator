using System.Collections.Concurrent;
using AiOrchestrator.BuildingBlocks.Agents;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// The sandboxes this process is currently executing Runs in (#304): one entry per executing Run,
/// written when its sandbox is created and removed when it is disposed.
/// <para>
/// <see cref="RunPreviewHost"/>'s sibling, deliberately down to the shape. In memory for the same
/// reason: a stored row would outlive the sandbox it names and lie after a restart, and a name that
/// no longer resolves is worse than no name — it points a terminal at a microVM that is gone.
/// </para>
/// <para>
/// The name was a local variable in <see cref="Sbx.SbxAgentProcessHost"/> until this existed, which
/// is why no surface could address a sandbox. It is published here and nowhere else: there is no way
/// to add an entry except beside creating a sandbox, and no way to remove one except beside disposing
/// it. That is what makes "a finished Run has no terminal" a property rather than a branch somebody
/// has to remember to write.
/// </para>
/// </summary>
public sealed class RunSandboxHost : IRunSandboxMonitor
{
    readonly ConcurrentDictionary<Guid, string> _sandboxes = new();

    public bool Hosted => true;

    public string? NameFor(Guid runId) =>
        _sandboxes.TryGetValue(runId, out var sandbox) ? sandbox : null;

    /// <summary>This Run's sandbox exists and is addressable until it is disposed.</summary>
    public void Created(Guid runId, string sandbox) => _sandboxes[runId] = sandbox;

    /// <summary>The sandbox is gone, so its name is. Idempotent — disposal can race a crash.</summary>
    public void Gone(Guid runId) => _sandboxes.TryRemove(runId, out _);
}
