using System.Collections.Concurrent;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiOrchestrator.Modules.Runs.Features.Observation;

/// <summary>
/// A shell in an executing Run's sandbox, delivered to a browser (#304). The live log window's
/// sibling in transport and its opposite in kind: that one is a window on a record, this one
/// executes commands on the machine a Run is using.
/// <para>
/// <b>Authorization is checked here, per Run, as <see cref="RunLogHub"/> had to learn.</b> A hub
/// dispatches nothing through the CQS pipeline, so the decorator that guards every other read never
/// sees it. The log hub's comment explains why that mattered even for reading; this surface writes,
/// so the same omission would hand any signed-in caller a shell.
/// </para>
/// <para>
/// It asks for <see cref="RunPermissions.Attach"/> and not <see cref="RunPermissions.Read"/>, because
/// reading a Run observes what happened while this runs arbitrary commands as whoever the sandbox
/// authenticates as (#288).
/// </para>
/// <para>
/// One terminal per connection, and one connection per Run: a second viewer is refused rather than
/// silently sharing a workspace with the first, which is a coordination problem this slice does not
/// own.
/// </para>
/// </summary>
sealed class RunTerminalHub(
    RunsDbContext database,
    IProjectPermissions permissions,
    IOptions<PermissionGrants> grants,
    IRunTerminalHost terminals,
    IRunAttachRecorder attaches,
    ICurrentPrincipal principal
) : Hub
{
    /// <summary>
    /// The terminals this process is pumping, one per connection. In memory and per process for the
    /// same reason the sandbox ledger is: a terminal exists while its connection does.
    /// </summary>
    static readonly ConcurrentDictionary<string, IRunTerminal> Terminals = new();

    /// <summary>Which Run each connection is attached to, so a second viewer can be refused.</summary>
    static readonly ConcurrentDictionary<Guid, string> Attached = new();

    /// <summary>
    /// Opens the shell at the caller's window size and starts pumping it. The size is fixed for the
    /// life of the terminal — resizing a live pseudo-terminal needs a system call .NET cannot make —
    /// and the surface tells the reader so rather than leaving them waiting for a reflow.
    /// </summary>
    public async Task Open(Guid runId, int columns, int rows)
    {
        var run = await database
            .Runs.Where(candidate => candidate.Id == runId)
            .Select(candidate => new { candidate.ProjectId, candidate.State })
            .FirstOrDefaultAsync(Context.ConnectionAborted);

        // The same two questions, in the same order, against the same table as every other read of a
        // Run — so a terminal and a log cannot disagree about who may. One refusal for "no such Run"
        // and "somebody else's project", because telling them apart enumerates Runs.
        var role = run is null
            ? null
            : await permissions.RoleOn(run.ProjectId, Context.ConnectionAborted);

        if (role is null || !grants.Value.Holds(role.Value, RunPermissions.Attach))
        {
            throw new HubException("You do not have permission to open a terminal on this run.");
        }

        // Three refusals, each with its own reason (design D5). A habitat that hosts no terminal is
        // not a permission problem and must never read as one: asking for access would not help.
        if (!terminals.Hosted)
        {
            throw new HubException(
                "No terminal is hosted in this habitat. A Run's sandbox can be opened where the "
                    + "agent runs on this machine, and not where it runs in the cloud."
            );
        }

        if (run!.State != RunState.Executing)
        {
            throw new HubException(
                "This run is not executing, so it has no sandbox to open a terminal in."
            );
        }

        if (Attached.TryGetValue(runId, out var holder) && Terminals.ContainsKey(holder))
        {
            throw new HubException(
                "Somebody already has a terminal open on this run. Two shells in one workspace is "
                    + "not something this supports yet."
            );
        }

        var terminal =
            terminals.Open(runId, columns, rows)
            ?? throw new HubException(
                "This run's sandbox is gone, so there is nothing to open a terminal in."
            );

        Terminals[Context.ConnectionId] = terminal;
        Attached[runId] = Context.ConnectionId;

        // Who opened it and when, against the Run (#304 criterion 6). The terminal's own bytes are
        // deliberately NOT recorded: the Run's transcript stays the agent's record rather than
        // becoming a screen capture of somebody's shell.
        await attaches.Attached(
            runId,
            principal.Current.DisplayName,
            DateTimeOffset.UtcNow,
            Context.ConnectionAborted
        );

        // Pumped on a background task rather than awaited: this call must return so the client can
        // start typing, and the read blocks until the shell says something.
        _ = Pump(Clients.Caller, terminal, Context.ConnectionId, runId);
    }

    /// <summary>Keystrokes. Unchecked deliberately — see below.</summary>
    public Task Send(byte[] data)
    {
        // No authorization here, and that is not an omission: this writes to a terminal THIS
        // connection opened and passed the check for. A connection with no open terminal has nothing
        // to write to, which is the whole of the guard.
        if (Terminals.TryGetValue(Context.ConnectionId, out var terminal))
        {
            terminal.Write(data);
        }

        return Task.CompletedTask;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        Close(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    static async Task Pump(
        ISingleClientProxy client,
        IRunTerminal terminal,
        string connection,
        Guid runId
    )
    {
        var buffer = new byte[8192];

        try
        {
            while (true)
            {
                var read = terminal.Read(buffer);
                if (read == 0)
                {
                    // The shell ended, or the sandbox went with its Run. Both are how a terminal
                    // finishes, and the client is told so it can say the sandbox is gone rather than
                    // leaving a dead terminal on screen.
                    await client.SendAsync("ended");
                    return;
                }

                await client.SendAsync("output", buffer[..read]);
            }
        }
        // The tab closed mid-write, or the connection went with it. Each of these is how a terminal
        // ordinarily ends rather than a fault, and naming them is what keeps a real fault visible.
        catch (OperationCanceledException)
        {
            // The connection was aborted.
        }
        catch (ObjectDisposedException)
        {
            // The terminal was disposed by a racing disconnect.
        }
        catch (IOException)
        {
            // The client went away mid-frame.
        }
        finally
        {
            Close(connection);
            Attached.TryRemove(runId, out _);
        }
    }

    static void Close(string connection)
    {
        if (Terminals.TryRemove(connection, out var terminal))
        {
            // Kills the shell and the sbx CLI holding the exec: a closed browser tab must not leave a
            // process running inside somebody's sandbox.
            terminal.Dispose();
        }

        foreach (var pair in Attached.Where(pair => pair.Value == connection))
        {
            Attached.TryRemove(pair.Key, out _);
        }
    }
}
