using AiOrchestrator.BuildingBlocks.Agents;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents.Sbx;

/// <summary>
/// Opens a shell in a Run's sbx sandbox (#304) — the self-host half of ADR-0021, and the only half
/// that exists: a deployment keeps <see cref="UnhostedRunTerminalHost"/>.
/// <para>
/// It resolves the sandbox from the ledger and nowhere else, so a terminal can only ever be opened on
/// a Run whose sandbox is alive. There is no path by which a caller-supplied name reaches
/// <c>sbx exec</c>, which is what stops this becoming a way to enter any sandbox on the machine.
/// </para>
/// </summary>
sealed class SbxRunTerminalHost(
    SbxSandboxOptions options,
    RunSandboxHost sandboxes,
    ILogger<SbxRunTerminalHost> logger
) : IRunTerminalHost
{
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

    /// <summary>The pty as the module sees it: bytes, and a way to stop.</summary>
    sealed class SbxRunTerminal(InteractivePty pty) : IRunTerminal
    {
        public int Read(byte[] buffer) => pty.Read(buffer);

        public void Write(ReadOnlySpan<byte> data) => pty.Write(data);

        public void Dispose() => pty.Dispose();
    }
}
