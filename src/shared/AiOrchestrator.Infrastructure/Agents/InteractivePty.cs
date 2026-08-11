using System.Runtime.InteropServices;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// A command running on a pseudo-terminal, for as long as somebody is typing at it (#304).
/// <see cref="HeadlessProcess"/>'s opposite number: that one captures two streams and kills on a
/// deadline (BR-005), this one holds a duplex byte stream open and has no deadline of its own,
/// because the thing on the other end is a person and BR-006 puts no clock on a person.
/// <para>
/// <b>Why not <see cref="System.Diagnostics.Process"/>.</b> `sbx exec -it` refuses a redirected
/// pipe — it dies with `ERROR: inspect exec: context deadline exceeded`, not a tty-shaped message —
/// so the child needs a real terminal, and `Process.Start` offers no way to hand a child an
/// arbitrary file descriptor. Hence `openpty` for the terminal and `posix_spawn` for the child, both
/// measured before this was written (`poc/PtyCheck.cs`).
/// </para>
/// <para>
/// <b>Unix only, deliberately.</b> This runs in the self-host habitat and nowhere else (ADR-0021),
/// which is a developer's own macOS or Linux machine, because that is where sbx runs. A Windows arm
/// would be dead code guarding a capability that habitat does not have.
/// </para>
/// <para>
/// <b>The size is fixed when the terminal opens</b> and cannot be changed afterwards, which is a
/// decision rather than an omission: resizing a live pty needs `ioctl`, `ioctl` is variadic, and
/// .NET refuses variadic calls outright (`Vararg calling convention not supported`, measured). The
/// geometry is passed to `openpty` instead, and the surface tells the reader that a window resize
/// will not reflow an open terminal.
/// </para>
/// </summary>
sealed class InteractivePty : IDisposable
{
    const int StdIn = 0;
    const int StdOut = 1;
    const int StdErr = 2;

    readonly int _master;
    int _pid;
    bool _disposed;

    InteractivePty(int master, int pid)
    {
        _master = master;
        _pid = pid;
    }

    /// <summary>
    /// Starts <paramref name="fileName"/> on a new pseudo-terminal sized to the caller's window.
    /// Throws <see cref="AgentProcessHostException"/> when the terminal or the child cannot be
    /// created, because a caller holding a half-open terminal has nothing useful to do with it.
    /// </summary>
    public static InteractivePty Start(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        int columns,
        int rows
    )
    {
        // openpty takes the initial size, which is the whole reason the size is settled here and
        // never again: this argument needs no variadic call, and ioctl would.
        var size = new Winsize
        {
            Rows = (ushort)Math.Clamp(rows, 1, ushort.MaxValue),
            Columns = (ushort)Math.Clamp(columns, 1, ushort.MaxValue),
        };

        var sizeBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<Winsize>());
        var actions = IntPtr.Zero;
        var pointers = new List<IntPtr>();

        try
        {
            Marshal.StructureToPtr(size, sizeBuffer, false);

            if (openpty(out var master, out var slave, IntPtr.Zero, IntPtr.Zero, sizeBuffer) != 0)
            {
                throw new AgentProcessHostException(
                    "A terminal could not be created on this machine, so no shell can be opened in "
                        + $"the Run's sandbox. (openpty: errno {Marshal.GetLastWin32Error()})"
                );
            }

            // posix_spawn_file_actions_t is an opaque POINTER on macOS and a struct on Linux;
            // allocating a buffer and passing its address is correct for both.
            actions = Marshal.AllocHGlobal(256);
            var initialised = posix_spawn_file_actions_init(actions);
            if (initialised != 0)
            {
                close(master);
                close(slave);
                throw new AgentProcessHostException(
                    $"A terminal could not be prepared for the shell. (rc {initialised})"
                );
            }

            posix_spawn_file_actions_adddup2(actions, slave, StdIn);
            posix_spawn_file_actions_adddup2(actions, slave, StdOut);
            posix_spawn_file_actions_adddup2(actions, slave, StdErr);

            // argv[0] is the program's own name by convention, so the command is passed twice.
            var argv = Terminated(pointers, [fileName, .. arguments]);
            // INHERIT, then overlay — not replace. `posix_spawn` takes the child's whole environment,
            // where `Process.Start` starts from a copy of this process's; matching HeadlessProcess's
            // contract ("these entries in addition") is what the callers already assume.
            //
            // Measured the hard way: passing only the caller's entries made the sbx CLI die with
            // `panic: $HOME is not defined` before any sandbox was touched. The CLI runs on THIS
            // machine, so inheriting is also what the sandbox boundary expects — nothing here
            // crosses into the sandbox, which is why SbxSandboxLifecycle hands this seam nothing.
            var merged = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (
                System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables()
            )
            {
                if (entry.Key is string key && entry.Value is string value)
                {
                    merged[key] = value;
                }
            }

            foreach (var pair in environment)
            {
                merged[pair.Key] = pair.Value;
            }

            var envp = Terminated(pointers, [.. merged.Select(pair => $"{pair.Key}={pair.Value}")]);

            // posix_spawnp RETURNS its error number and does not set errno — reading errno here
            // reports 0 on a real failure, which is how a marshalling bug hid for a whole run.
            var spawned = posix_spawnp(out var pid, fileName, actions, IntPtr.Zero, argv, envp);
            if (spawned != 0)
            {
                close(master);
                close(slave);
                throw new AgentProcessHostException(
                    $"The shell could not be started in the sandbox. (posix_spawnp: rc "
                        + $"{spawned} for '{fileName}' — 2 means the command was not found on PATH)"
                );
            }

            // The parent's copy of the slave must go, or reading the master never sees the child's
            // exit — the read would block forever on a terminal nobody is writing to.
            close(slave);

            return new InteractivePty(master, pid);
        }
        finally
        {
            Marshal.FreeHGlobal(sizeBuffer);

            if (actions != IntPtr.Zero)
            {
                posix_spawn_file_actions_destroy(actions);
                Marshal.FreeHGlobal(actions);
            }

            foreach (var pointer in pointers)
            {
                Marshal.FreeHGlobal(pointer);
            }
        }
    }

    /// <summary>
    /// Reads whatever the terminal has produced, blocking until there is something or the child is
    /// gone. Returns 0 at end of stream — the shell exited, or the sandbox went away underneath it.
    /// </summary>
    public int Read(byte[] buffer)
    {
        var read = (int)read_(_master, buffer, buffer.Length);

        // EIO on a master whose child has exited is the ordinary end of a terminal on Linux, not a
        // fault; treating it as end-of-stream is what makes "the shell exited" and "the reader
        // stopped" the same shape for the caller.
        return read < 0 ? 0 : read;
    }

    /// <summary>Writes keystrokes to the terminal. Control characters arrive as signals.</summary>
    public void Write(ReadOnlySpan<byte> data)
    {
        var buffer = data.ToArray();
        var written = 0;

        while (written < buffer.Length)
        {
            var wrote = (int)write_(
                _master,
                buffer.AsSpan(written).ToArray(),
                buffer.Length - written
            );
            if (wrote <= 0)
            {
                // The terminal is gone. The reader will see end-of-stream and the caller closes;
                // throwing here would only turn a closed tab into an error.
                return;
            }

            written += wrote;
        }
    }

    /// <summary>
    /// Ends the session: the child, then the terminal. The child is signalled rather than waited
    /// for, because whoever is closing this is a browser tab that has already gone.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_pid > 0)
        {
            // SIGHUP first, as a hang-up on a terminal means: the sbx CLI holding the exec passes it
            // on to what it started, which is what stops a shell surviving its viewer.
            kill(_pid, SIGHUP);
            kill(_pid, SIGKILL);
            waitpid(_pid, out _, 0);
            _pid = 0;
        }

        close(_master);
    }

    static IntPtr[] Terminated(List<IntPtr> owned, string[] values)
    {
        // A NULL terminator, which .NET's own string[] marshalling does not add — without it the
        // callee reads past the end and answers EFAULT (measured).
        var pointers = new IntPtr[values.Length + 1];

        for (var i = 0; i < values.Length; i++)
        {
            pointers[i] = Marshal.StringToHGlobalAnsi(values[i]);
            owned.Add(pointers[i]);
        }

        pointers[^1] = IntPtr.Zero;
        return pointers;
    }

    const int SIGHUP = 1;
    const int SIGKILL = 9;

    [DllImport("libc", SetLastError = true)]
    static extern int openpty(
        out int master,
        out int slave,
        IntPtr name,
        IntPtr termios,
        IntPtr winsize
    );

    [DllImport("libc", SetLastError = true)]
    static extern int close(int fd);

    [DllImport("libc", SetLastError = true, EntryPoint = "read")]
    static extern nint read_(int fd, byte[] buffer, nint count);

    [DllImport("libc", SetLastError = true, EntryPoint = "write")]
    static extern nint write_(int fd, byte[] buffer, nint count);

    [DllImport("libc", SetLastError = true)]
    static extern int kill(int pid, int signal);

    [DllImport("libc", SetLastError = true)]
    static extern int waitpid(int pid, out int status, int options);

    /// <summary>
    /// <c>posix_spawn<b>p</b></c>, with the <c>p</c>: it searches <c>PATH</c> for a bare command name,
    /// where plain <c>posix_spawn</c> requires an absolute path and answers ENOENT for anything else.
    /// <para>
    /// The <c>p</c> was missing until #311 exercised the terminal on a machine where <c>sbx</c> is on
    /// <c>PATH</c> but not at a configured absolute path — the default <c>CommandPath</c> is the bare
    /// name <c>"sbx"</c>. The listing worked and the terminal did not, because
    /// <see cref="HeadlessProcess"/> starts its child through <c>ProcessStartInfo</c>, which resolves
    /// <c>PATH</c> for you. Two ways of starting the same binary that disagreed about how to find it.
    /// </para>
    /// </summary>
    [DllImport("libc", SetLastError = true)]
    static extern int posix_spawnp(
        out int pid,
        string file,
        IntPtr fileActions,
        IntPtr attr,
        IntPtr[] argv,
        IntPtr[] envp
    );

    [DllImport("libc", SetLastError = true)]
    static extern int posix_spawn_file_actions_init(IntPtr actions);

    [DllImport("libc", SetLastError = true)]
    static extern int posix_spawn_file_actions_destroy(IntPtr actions);

    [DllImport("libc", SetLastError = true)]
    static extern int posix_spawn_file_actions_adddup2(IntPtr actions, int fd, int newfd);

    [StructLayout(LayoutKind.Sequential)]
    struct Winsize
    {
        public ushort Rows;
        public ushort Columns;
        public ushort XPixel;
        public ushort YPixel;
    }
}
