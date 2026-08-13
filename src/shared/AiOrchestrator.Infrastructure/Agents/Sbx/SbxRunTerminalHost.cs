using AiOrchestrator.BuildingBlocks.Agents;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents.Sbx;

/// <summary>
/// Opens a shell in an sbx sandbox on this machine — the self-host half of ADR-0021, and the only half
/// that exists: a deployment keeps <see cref="UnhostedRunTerminalHost"/>.
/// <para>
/// <b>Two ways in, and what bounds each.</b> By Run id (#304) the sandbox is resolved from the ledger, so
/// a terminal can only be opened on a Run whose sandbox is alive. By name (#311) a caller does supply the
/// string — deliberately, because the sandboxes worth reaching include those no Run owns any more — and
/// what stops that becoming a way into any sandbox on the machine is <see cref="SbxSandboxRoster"/>: the
/// name is resolved against a freshly-read listing of the namespace this host claims, and refused when it
/// is not there.
/// </para>
/// <para>
/// <b>#304 asserted the opposite invariant</b> — "there is no path by which a caller-supplied name reaches
/// <c>sbx exec</c>" — and that sentence is gone rather than quietly falsified. The bound moved from the
/// ledger to the namespace; it did not disappear. A comment claiming an invariant the code no longer holds
/// would be worse than none, because the next reader would trust it.
/// </para>
/// </summary>
sealed class SbxRunTerminalHost(
    SbxSandboxOptions options,
    RunSandboxHost sandboxes,
    ILogger<SbxRunTerminalHost> logger
) : IRunTerminalHost
{
    /// <summary>
    /// Built from options rather than injected, as every other sbx collaborator is: the CLI seam is
    /// composition detail and stays out of the container (design D7 of the sbx work).
    /// </summary>
    readonly SbxCli _cli = new(options);

    public bool Hosted => true;

    public IRunTerminal? Open(Guid runId, int columns, int rows)
    {
        if (sandboxes.NameFor(runId) is not { } sandbox)
        {
            // Not an error: the ordinary answer for a Run that has finished, or one that never had a
            // sandbox. The surface renders it as "no terminal", never as a failure.
            return null;
        }

        // No --workdir: sbx starts a shell in the workspace it mounted, which is the Run's own
        // repository — the directory a human attaching almost always wants. `-it` is what makes this
        // a terminal, and it is exactly what refuses a plain pipe, which is why the pty exists.
        var pty = InteractivePty.Start(
            options.CommandPath,
            ["exec", "-it", sandbox, "bash"],
            // Nothing of ours: the CLI runs on this machine and inherits what it needs. TERM is the
            // one addition, because a shell with no TERM draws nothing worth looking at.
            new Dictionary<string, string> { ["TERM"] = "xterm-256color" },
            columns,
            rows
        );

        SandboxLog.TerminalOpened(logger, sandbox);
        return new SbxRunTerminal(pty);
    }

    public async Task<IReadOnlyList<TerminalTarget>> List(CancellationToken cancellationToken)
    {
        var claimed = await SbxSandboxRoster.Claimed(_cli, cancellationToken);

        return
        [
            .. claimed.Select(entry => new TerminalTarget(
                entry.Name,
                entry.Status,
                // Attribution, not identity: null means no Run of this process is using it, which is
                // the ordinary state of a sandbox an earlier process abandoned.
                sandboxes.RunUsing(entry.Name),
                entry.Workspace
            )),
        ];
    }

    public async Task<IRunTerminal?> Open(
        string sandbox,
        int columns,
        int rows,
        CancellationToken cancellationToken
    )
    {
        // Re-read rather than trust. The caller's name came from a listing they read at some earlier
        // moment, and between then and now the reaper may have taken that sandbox and a new Run may
        // have been given the name. Resolving here is the whole of the namespace bound.
        var claimed = await SbxSandboxRoster.Claimed(_cli, cancellationToken);
        var resolved = claimed.FirstOrDefault(entry =>
            string.Equals(entry.Name, sandbox, StringComparison.Ordinal)
        );

        if (resolved is null)
        {
            // Not an error, and not distinguishable from "no such sandbox" on purpose: telling a caller
            // that a name exists but is not ours would enumerate the machine for them.
            return null;
        }

        // A stopped sandbox is started by this, which the surface has already told the caller (design
        // D5). Verified against the real CLI: `sbx exec` on a stopped sandbox starts it and then runs.
        var pty = InteractivePty.Start(
            options.CommandPath,
            ["exec", "-it", resolved.Name, "bash"],
            new Dictionary<string, string> { ["TERM"] = "xterm-256color" },
            columns,
            rows
        );

        SandboxLog.TerminalOpened(logger, resolved.Name);
        return new SbxRunTerminal(pty);
    }

    /// <summary>The pty as the module sees it: bytes, and a way to stop.</summary>
    sealed class SbxRunTerminal(InteractivePty pty) : IRunTerminal
    {
        public int Read(byte[] buffer) => pty.Read(buffer);

        public void Write(ReadOnlySpan<byte> data) => pty.Write(data);

        public void Dispose() => pty.Dispose();
    }
}
