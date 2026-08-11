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
}

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
}
