using AiOrchestrator.BuildingBlocks.Agents;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// A shell on <b>this machine</b>, in an executing Run's own checkout — the terminal for a habitat that
/// runs agents as children of this process and has no sandbox to enter (#358, DEC-070).
/// <para>
/// <see cref="Sbx.SbxRunTerminalHost"/>'s counterpart, and the reason it needed one: a terminal was a
/// property of the sbx launcher rather than of locality, because composition registered
/// <see cref="IRunTerminalHost"/> only in the sbx branch. The one habitat ADR-0021 permits attaching in
/// was the one habitat with no terminal.
/// </para>
/// <para>
/// <b>The bounds are the decision, not decoration</b> (DEC-070). The shell starts in the Run's checkout;
/// it does <b>not</b> inherit this process's environment; and the shell is a named one rather than the
/// operator's login shell. Each is a requirement with a reason, written beside where it is applied.
/// </para>
/// <para>
/// <b>What this is not.</b> The bound is a product boundary, not a kernel one — a shell in the checkout
/// can still leave it. DEC-070 says so plainly and so does this class: nothing here isolates anything,
/// and a habitat that needs isolation names the sbx launcher instead. Reading this type as a sandbox
/// would be the specific mistake the decision warns about.
/// </para>
/// </summary>
sealed class LocalRunTerminalHost(RunCheckoutHost checkouts) : IRunTerminalHost
{
    /// <summary>
    /// The shell the terminal starts. Named rather than <c>$SHELL</c>: what opens should be a property
    /// of the product, not of whatever is in the operator's profile — a login shell would also source
    /// that profile and re-import the environment this host deliberately withholds.
    /// </summary>
    const string Shell = "/bin/bash";

    public bool Hosted => true;

    public IRunTerminal? Open(Guid runId, int columns, int rows)
    {
        // Null when this Run has no checkout — it is not executing, or it is not a Local Run. The
        // surface turns that into "no terminal for this Run", which is a different sentence from this
        // habitat hosting none, and both already exist.
        if (checkouts.NameFor(runId) is not { Length: > 0 } checkout)
        {
            return null;
        }

        return Start(checkout);
    }

    /// <summary>
    /// Every checkout this process has a Run in. Synchronous underneath, unlike the sandbox host's, and
    /// the difference is honest: a sandbox listing asks the machine and is stale the instant it returns,
    /// while a checkout is only knowable from this process's own ledger. Stated in
    /// <see cref="RunCheckoutHost.Targets"/> rather than hidden behind the shared signature.
    /// </summary>
    public Task<IReadOnlyList<TerminalTarget>> List(CancellationToken cancellationToken) =>
        Task.FromResult(checkouts.Targets());

    public Task<IRunTerminal?> Open(
        string sandbox,
        int columns,
        int rows,
        CancellationToken cancellationToken
    )
    {
        // Resolved against the ledger rather than trusted, for the reason the seam documents: a
        // caller's name is a memory of a listing they read earlier, and the Run may have finished and
        // its checkout been reaped since. Passing the path straight through would let a stale name open
        // a shell in whatever now sits at that path.
        if (checkouts.RunUsing(sandbox) is null)
        {
            return Task.FromResult<IRunTerminal?>(null);
        }

        return Task.FromResult<IRunTerminal?>(Start(sandbox));
    }

    static LocalTerminal Start(string checkout) =>
        new(
            InteractivePty.Start(
                Shell,
                // Interactive so the shell draws a prompt, and `--noprofile --norc` so it does not
                // source the operator's startup files — which would undo the environment bound below
                // by re-exporting whatever their profile exports.
                ["--noprofile", "--norc", "-i"],
                // The whole environment this shell gets. PATH so ordinary commands resolve, HOME
                // because a shell without one misbehaves in ways that look like product bugs, and TERM
                // so cursor addressing works at all. Nothing from this process: that is the bound.
                new Dictionary<string, string>
                {
                    ["PATH"] = "/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin",
                    ["HOME"] = checkout,
                    ["TERM"] = "xterm-256color",
                    // Said in the shell itself, because a person who opened this from a browser has no
                    // other way to know which of the two terminals they are in.
                    ["PS1"] = "[run] \\W $ ",
                },
                columns: 120,
                rows: 30,
                workingDirectory: checkout,
                inheritEnvironment: false
            )
        );

    /// <summary>
    /// One open shell on this machine. A thin adapter rather than a second implementation: the
    /// pseudo-terminal is the same one the sandbox path uses, which is what keeps "a terminal" one thing
    /// in this product rather than two that drift.
    /// </summary>
    sealed class LocalTerminal(InteractivePty pty) : IRunTerminal
    {
        public int Read(byte[] buffer) => pty.Read(buffer);

        public void Write(ReadOnlySpan<byte> data) => pty.Write(data);

        public void Dispose() => pty.Dispose();
    }
}
