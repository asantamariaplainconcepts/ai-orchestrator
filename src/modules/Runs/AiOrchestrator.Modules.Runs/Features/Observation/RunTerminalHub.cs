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
    IRunSandboxMonitor sandboxes,
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
    /// The same guard for sandboxes reached by name (#311), keyed by sandbox because that surface has no
    /// Run to key by. Separate from <see cref="Attached"/> rather than unified: two shells in one
    /// workspace is the thing being refused, and a sandbox is what a workspace belongs to.
    /// </summary>
    static readonly ConcurrentDictionary<string, string> AttachedSandboxes = new(
        StringComparer.Ordinal
    );

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

        // Read before opening, because the record needs the name and the ledger is what has it. If it
        // has gone in the meantime, Open answers null below and the attach is never recorded.
        var named = sandboxes.NameFor(runId);

        var terminal =
            terminals.Open(runId, columns, rows)
            ?? throw new HubException(
                "This run's sandbox is gone, so there is nothing to open a terminal in."
            );

        Terminals[Context.ConnectionId] = terminal;
        Attached[runId] = Context.ConnectionId;

        // Who opened it, when, and which sandbox (#304 criterion 6, #311 criterion 7). The terminal's
        // own bytes are deliberately NOT recorded: the Run's transcript stays the agent's record rather
        // than becoming a screen capture of somebody's shell.
        await attaches.Attached(
            named ?? "(unknown)",
            runId,
            principal.Current.DisplayName,
            DateTimeOffset.UtcNow,
            Context.ConnectionAborted
        );

        // Pumped off this call rather than awaited: it must return so the client can start typing, and
        // the read blocks until the shell says something. Scheduled through the one helper both entry
        // points use — see StartPump, which is also where the thread choice is argued.
        StartPump(() => Pump(Clients.Caller, terminal, Context.ConnectionId, runId));
    }

    /// <summary>
    /// Opens a shell in one of this machine's own sandboxes, by name (#311) — the entry point that is not
    /// keyed to a Run, for the sandboxes no Run owns.
    /// <para>
    /// <b>The refusals are ordered, and the order is a requirement.</b> The habitat answers first, so a
    /// deployment never evaluates a permission for a surface it does not host — and so its answer can
    /// never read as a permission a Member could ask to be given. Permission comes second. The name
    /// comes last, and a name outside the claimed namespace is refused identically to a name that exists
    /// nowhere, because telling those apart would let a caller enumerate the machine.
    /// </para>
    /// </summary>
    public async Task OpenSandbox(string sandbox, int columns, int rows)
    {
        // First, and before anything about the caller. ADR-0021: the ability is absent in a deployment
        // rather than present and refused.
        if (!terminals.Hosted)
        {
            throw new HubException(
                "No terminal is hosted in this habitat. This machine's sandboxes can be opened where "
                    + "the agent runs on this machine, and not where it runs in the cloud."
            );
        }

        if (
            !await MachineTerminalAccess.MayAttachSomewhere(
                permissions,
                grants.Value,
                Context.ConnectionAborted
            )
        )
        {
            throw new HubException(
                "You do not have permission to open a terminal on this machine's sandboxes."
            );
        }

        if (AttachedSandboxes.TryGetValue(sandbox, out var holder) && Terminals.ContainsKey(holder))
        {
            throw new HubException(
                "Somebody already has a terminal open on this sandbox. Two shells in one workspace is "
                    + "not something this supports yet."
            );
        }

        // The host re-resolves the name against a fresh listing and answers null for anything that is
        // not this machine's to enter — including a sandbox that has been reaped since the caller read
        // the list.
        var terminal =
            await terminals.Open(sandbox, columns, rows, Context.ConnectionAborted)
            ?? throw new HubException(
                "That sandbox is not this machine's to open. It may have been removed, or it may not be "
                    + "one this product created."
            );

        Terminals[Context.ConnectionId] = terminal;
        AttachedSandboxes[sandbox] = Context.ConnectionId;

        // Recorded whether or not a Run owns it — the attaches least reconstructable afterwards are
        // exactly the ones on sandboxes no Run owns (#311 criterion 7).
        await attaches.Attached(
            sandbox,
            sandboxes.RunUsing(sandbox),
            principal.Current.DisplayName,
            DateTimeOffset.UtcNow,
            Context.ConnectionAborted
        );

        // Off this call for the reason the Run-keyed path spells out: the first act of the pump is a
        // blocking read, and running it here means `OpenSandbox` never returns. The same helper, so the
        // two entry points cannot drift into two answers for one question (#330 criterion 2).
        StartPump(() => PumpSandbox(Clients.Caller, terminal, Context.ConnectionId, sandbox));
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

    static void Pump(
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
                    Send(client, "ended");
                    return;
                }

                Send(client, "output", buffer[..read]);
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

    /// <summary>
    /// <see cref="Pump"/> for a sandbox-keyed terminal. The same loop and the same endings — the shell
    /// exited, or the sandbox was disposed underneath it, which are one event to whoever is watching
    /// (#311 criterion 6) — differing only in which guard it releases.
    /// </summary>
    static void PumpSandbox(
        ISingleClientProxy client,
        IRunTerminal terminal,
        string connection,
        string sandbox
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
                    Send(client, "ended");
                    return;
                }

                Send(client, "output", buffer[..read]);
            }
        }
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
            AttachedSandboxes.TryRemove(sandbox, out _);
        }
    }

    /// <summary>
    /// Starts a terminal pump on a thread <b>dedicated to it</b> (#330). One helper, both entry points,
    /// because two call sites answering one question is how they drift — and any future entry point
    /// starts its pump here too.
    /// <para>
    /// <b>Why not the thread pool.</b> The pool is sized for work that finishes; a pump never does. Its
    /// first act is a blocking <c>Read</c> that does not return until the shell speaks, and the loop
    /// blocks again for as long as the terminal lives — so every open terminal permanently occupied a
    /// pool worker, competing with every request the process serves. The pool grows by one or two
    /// threads a second, so a burst of terminals could also delay unrelated work from starting.
    /// </para>
    /// <para>
    /// <b>Why not <c>TaskCreationOptions.LongRunning</c>, which is the obvious answer and the wrong
    /// one.</b> Measured 2026-08-13: <c>Task.Factory.StartNew(Func&lt;Task&gt;, …, LongRunning)</c> runs
    /// the delegate on a dedicated thread only until its first <i>suspending</i> await. At that point the
    /// delegate returns its <c>Task</c>, the dedicated thread completes its work item and exits, and
    /// every continuation — including <b>every subsequent blocking <c>Read</c></b> — resumes on a pool
    /// worker. A probe recording <c>Thread.CurrentThread.IsThreadPoolThread</c> either side of a
    /// suspending await reported <c>false</c> before and <c>true</c> after. So <c>LongRunning</c> moves
    /// the <i>first</i> read off the pool and nothing else, which is almost certainly why #327's
    /// experiment with it "changed nothing in CI" and was reverted.
    /// </para>
    /// <para>
    /// <b>So the loop is synchronous on a real thread</b>, and the sends block it (see <see cref="Send"/>).
    /// That is sync-over-async, entered deliberately: this thread exists to be blocked, it would
    /// otherwise be blocked inside <c>Read</c> anyway, and ASP.NET Core has no
    /// <c>SynchronizationContext</c> to deadlock against. The cost is one thread per open terminal, which
    /// is the honest price of a duplex stream that lives as long as somebody is watching it.
    /// </para>
    /// </summary>
    internal static void StartPump(Action pump) =>
        new Thread(pump.Invoke)
        {
            // Background, so a pump blocked on a read that will never return cannot hold up shutdown.
            IsBackground = true,
            Name = "run-terminal-pump",
        }.Start();

    /// <summary>
    /// Sends on the pump's own thread, blocking until it completes. Blocking is the point: this thread is
    /// dedicated to one terminal, and awaiting here would hand the rest of the loop — the next blocking
    /// read — back to the thread pool, which is the whole defect <see cref="StartPump"/> exists to fix.
    /// </summary>
    static void Send(ISingleClientProxy client, string method, object? argument = null) =>
        (argument is null ? client.SendAsync(method) : client.SendAsync(method, argument))
            .GetAwaiter()
            .GetResult();

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

        foreach (var pair in AttachedSandboxes.Where(pair => pair.Value == connection))
        {
            AttachedSandboxes.TryRemove(pair.Key, out _);
        }
    }
}
