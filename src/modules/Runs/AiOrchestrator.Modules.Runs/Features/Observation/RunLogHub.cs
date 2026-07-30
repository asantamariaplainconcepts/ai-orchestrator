using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

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
/// <para>
/// <b>Watching is checked here, per Run (#13).</b> Every other read of a Run's log goes through the
/// CQS pipeline and is refused there; this one dispatches nothing, so it declared nothing and the
/// authorization decorator never saw it. That was safe only while being authenticated implied being
/// permitted — every signed-in caller was Admin — and #13 is exactly what ended that. Left alone,
/// the slice that scoped every other read would have left the live stream of an agent's raw output
/// open to any signed-in caller who knew a Run id. A surface that dispatches nothing has to say this
/// for itself; found by reading how ds-connect gates surfaces its own pipeline cannot see.
/// </para>
/// </summary>
sealed class RunLogHub(RunsDbContext database, IProjectPermissions permissions) : Hub
{
    internal static string GroupFor(Guid runId) => $"run-log:{runId}";

    public async Task Watch(Guid runId)
    {
        var projectId = await database
            .Runs.Where(run => run.Id == runId)
            .Select(run => (Guid?)run.ProjectId)
            .FirstOrDefaultAsync(Context.ConnectionAborted);

        // A Run that does not exist and a Run in somebody else's project get the same refusal, for
        // the same reason the HTTP ones do: telling them apart is a way to enumerate Runs. Member is
        // enough — observing is what the bundle grants (ACT-002).
        if (
            projectId is null
            || await permissions.RoleOn(projectId.Value, Context.ConnectionAborted) is null
        )
        {
            throw new HubException("You do not have permission to watch this run.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(runId));
    }

    /// <summary>
    /// Unchecked, deliberately: leaving a group you were never in does nothing, and a refusal here
    /// would only make closing a page fail.
    /// </summary>
    public Task Unwatch(Guid runId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(runId));
}
