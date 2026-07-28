using Microsoft.AspNetCore.SignalR;

namespace AiOrchestrator.Modules.Runs.Features.Observation;

/// <summary>
/// The live window's delivery (#106). One group per Run: a viewer joins on open and leaves on
/// close, and the portal pushes into the group when Postgres says new chunks landed.
/// <para>
/// Nothing writes to this hub but the portal itself (design D1) — the worker never connects,
/// which is why there is no ingest credential to hold and no ingress to protect. The hub is a
/// window on a record that exists without it: if it never delivered a line, the Run would be
/// unaffected and the page would still show everything through its poll.
/// </para>
/// </summary>
sealed class RunLogHub : Hub
{
    internal static string GroupFor(Guid runId) => $"run-log:{runId}";

    public Task Watch(Guid runId) => Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(runId));

    public Task Unwatch(Guid runId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(runId));
}
