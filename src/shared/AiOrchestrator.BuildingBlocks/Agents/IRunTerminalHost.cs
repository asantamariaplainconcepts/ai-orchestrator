namespace AiOrchestrator.BuildingBlocks.Agents;

/// <summary>
/// Opens a shell inside an executing Run's sandbox (#304). The seam exists for the reason the
/// preview monitor's does, and more strictly: the Runs module offers the terminal and authorizes it,
/// and must never know what a sandbox is or how one is entered — it asks for a byte stream and gets
/// one, or is told this habitat has none.
/// <para>
/// ADR-0021 permits this in self-host and refuses it in a deployment, so the deployed habitat holds
/// an implementation that hosts nothing at all. That is the honest shape: the ability is absent
/// there, rather than present and failing.
/// </para>
/// </summary>
public interface IRunTerminalHost
{
    /// <summary>Whether this habitat can open a terminal at all.</summary>
    bool Hosted { get; }

    /// <summary>
    /// A shell inside this Run's sandbox, sized to the caller's window, or null when there is no
    /// sandbox to enter — the Run is not executing, or this habitat hosts none.
    /// <para>
    /// The size is fixed here and cannot change afterwards, which the caller must tell its reader:
    /// resizing a live pseudo-terminal needs a variadic system call .NET cannot make.
    /// </para>
    /// </summary>
    IRunTerminal? Open(Guid runId, int columns, int rows);

    /// <summary>
    /// Every sandbox on this machine that this product created (#311), whatever Run it belongs to and
    /// whether or not it belongs to one at all. Empty where no terminal is hosted.
    /// <para>
    /// Asynchronous where <see cref="Open(Guid, int, int)"/> is not, and the difference is the point: a
    /// Run's sandbox is resolved from an in-memory ledger, while this asks the machine. The answer is
    /// therefore a fact about the machine at the moment it was asked, and stale the instant after.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<LocalSandbox>> List(CancellationToken cancellationToken);

    /// <summary>
    /// A shell inside the named sandbox, or null when that name is not this machine's to enter — it
    /// names no sandbox, or one this product did not create.
    /// <para>
    /// <b>The name is resolved against a fresh listing before it is used, never passed through.</b> A
    /// caller's name is a memory of a listing they read earlier, and a sandbox may have been reaped and
    /// its name reused since. Resolving here is what keeps the namespace bound real rather than
    /// advisory.
    /// </para>
    /// </summary>
    Task<IRunTerminal?> Open(
        string sandbox,
        int columns,
        int rows,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// One of this machine's sandboxes as a surface sees it (#311).
/// <para>
/// <paramref name="Status"/> is the sandbox runtime's own word rather than a boolean, because
/// entering a stopped sandbox <b>starts</b> it and a reader has to be told that before they click.
/// <paramref name="RunId"/> is present only for a Run this process is executing: the ledger that
/// answers it is deliberately unpersisted, so a sandbox left behind by a previous process is a real
/// sandbox with no Run to attribute it to.
/// </para>
/// </summary>
public sealed record LocalSandbox(string Name, string Status, Guid? RunId, string? Workspace);

/// <summary>
/// One open shell: bytes in, bytes out, and gone when disposed. Deliberately not a stream pair —
/// a terminal is one duplex thing, and splitting it would invite closing half of it.
/// </summary>
public interface IRunTerminal : IDisposable
{
    /// <summary>
    /// Fills <paramref name="buffer"/> with whatever the shell has produced, blocking until there is
    /// something. Returns 0 when the shell has ended — because it exited, or because the Run's
    /// sandbox went away underneath it, which are the same thing to whoever is watching.
    /// </summary>
    int Read(byte[] buffer);

    /// <summary>Sends keystrokes. Control characters arrive as signals, which is the point.</summary>
    void Write(ReadOnlySpan<byte> data);
}

/// <summary>
/// The default every habitat starts from, and the permanent answer in a deployment (ADR-0021).
/// Registered by the Runs module so the terminal's surface always resolves.
/// </summary>
public sealed class UnhostedRunTerminalHost : IRunTerminalHost
{
    public bool Hosted => false;

    public IRunTerminal? Open(Guid runId, int columns, int rows) => null;

    /// <summary>
    /// No sandboxes, because this habitat holds none — not an empty machine, an absent ability. The
    /// surface that calls this must answer from <see cref="Hosted"/> and never from the emptiness of
    /// this list, or a deployment would read as a machine that happens to have nothing on it.
    /// </summary>
    public Task<IReadOnlyList<LocalSandbox>> List(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LocalSandbox>>([]);

    public Task<IRunTerminal?> Open(
        string sandbox,
        int columns,
        int rows,
        CancellationToken cancellationToken
    ) => Task.FromResult<IRunTerminal?>(null);
}
