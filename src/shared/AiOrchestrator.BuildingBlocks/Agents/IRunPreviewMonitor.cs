namespace AiOrchestrator.BuildingBlocks.Agents;

/// <summary>
/// Which executing Runs currently have a preview to look at (run-previews design D2). The seam
/// exists for the same reason the pods and runtimes monitors do: the Runs module renders and
/// relays, and must never speak to a sandbox — it reads a snapshot, and the process that holds
/// the sandboxes keeps that snapshot true.
/// <para>
/// A process that holds no sandboxes answers <i>not hosted</i> rather than empty: "this Run has
/// no preview" and "previews are not available in this habitat" are different sentences, and a
/// Member reading the second as the first would think the Run failed to make one.
/// </para>
/// </summary>
public interface IRunPreviewMonitor
{
    /// <summary>
    /// The host port serving this Run's preview right now, or null when there is none — because
    /// the Run is not executing, because its Automation named no port, or because previews are
    /// not hosted here. <see cref="Hosted"/> distinguishes the last case.
    /// </summary>
    int? PortFor(Guid runId);

    /// <summary>Whether this process is the one that would hold previews at all.</summary>
    bool Hosted { get; }
}

/// <summary>
/// The default every habitat starts from; a host that launches sandboxes replaces it. Registered
/// by the Runs module so the relay's endpoint always resolves — the ability is absent, never the
/// answer.
/// </summary>
public sealed class UnhostedRunPreviewMonitor : IRunPreviewMonitor
{
    public int? PortFor(Guid runId) => null;

    public bool Hosted => false;
}
