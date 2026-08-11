#:sdk Microsoft.NET.Sdk

// Task 2.1 of human-opens-a-shell-in-a-run-sandbox: can .NET allocate a pty and give a CHILD
// process its slave end, then resize it live? Design D3 assumed a P/Invoke would do; ADR-0018 says
// a measurement licenses only what it measured, so this measures it.
//
// The hard part is not openpty. It is that Process.Start offers no way to hand a child an
// arbitrary file descriptor — RedirectStandardInput gives a pipe, and a pipe is exactly what
// `sbx exec -it` refuses. So the child must be spawned by posix_spawn with dup2 file actions.
//
// Run: dotnet run PtyCheck.cs

using System.Runtime.InteropServices;
using System.Text;

var failures = 0;

// openpty(3) lives in libutil on Linux and in libSystem (libc) on macOS. "libc" resolves to
// libSystem on macOS; the Linux arm is why the DllImport is duplicated below.
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

[DllImport("libc", SetLastError = true)]
static extern nint read(int fd, byte[] buffer, nint count);

// posix_spawn_file_actions_t is an OPAQUE POINTER on macOS and a struct on Linux. Passing the
// address of a heap buffer works for both: macOS writes a pointer into it, Linux initialises the
// struct in place. Measured, after marshalling it as an inline struct failed.
[DllImport("libc", SetLastError = true)]
static extern int posix_spawn(
    out int pid,
    string path,
    IntPtr fileActions,
    IntPtr attr,
    IntPtr[] argv,
    IntPtr[] envp
);

[DllImport("libc", SetLastError = true)]
static extern int posix_spawn_file_actions_init(IntPtr actions);

[DllImport("libc", SetLastError = true)]
static extern int posix_spawn_file_actions_adddup2(IntPtr actions, int fd, int newfd);

[DllImport("libc", SetLastError = true)]
static extern int waitpid(int pid, out int status, int options);

// TIOCSWINSZ differs per platform: 0x80087467 on macOS/BSD, 0x5414 on Linux.
var TIOCSWINSZ = OperatingSystem.IsMacOS() ? 0x80087467UL : 0x5414UL;

Console.WriteLine(
    $"platform: {RuntimeInformation.OSDescription} ({RuntimeInformation.ProcessArchitecture})"
);

// 1 — can we get a pty pair at all?
if (openpty(out var master, out var slave, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) != 0)
{
    Fail("openpty", $"errno {Marshal.GetLastWin32Error()}");
    return Report();
}

Ok("openpty returns a master/slave pair", $"master={master} slave={slave}");

// 2 — can a CHILD be given the slave as its stdio? This is the part Process.Start cannot do.
var actions = Marshal.AllocHGlobal(256);
var initRc = posix_spawn_file_actions_init(actions);
if (initRc != 0)
{
    Fail("posix_spawn_file_actions_init", $"rc {initRc}");
    return Report();
}

posix_spawn_file_actions_adddup2(actions, slave, 0);
posix_spawn_file_actions_adddup2(actions, slave, 1);
posix_spawn_file_actions_adddup2(actions, slave, 2);

// `tty` proves a controlling terminal; `stty size` proves the geometry we set below.
// posix_spawn takes `char *const argv[]` — a NULL-TERMINATED array of pointers. .NET's default
// string[] marshalling does not append that NULL, so the callee reads past the end and answers
// EFAULT (rc 14, measured). Marshalled by hand for the terminator.
var argv = Terminated(["sh", "-c", "tty; stty size; echo PTYCHECK-DONE"]);
var envp = Terminated(["TERM=xterm-256color", "PATH=/usr/bin:/bin:/usr/sbin:/sbin"]);

// Set the size BEFORE the child reads it, which is what `script` could not do.
var size = new Winsize
{
    Rows = 44,
    Cols = 137,
    XPixel = 0,
    YPixel = 0,
};
var sizePtr = Marshal.AllocHGlobal(Marshal.SizeOf<Winsize>());
Marshal.StructureToPtr(size, sizePtr, false);
var resized = Libc.ioctl(master, TIOCSWINSZ, __arglist(sizePtr));
Check("ioctl(TIOCSWINSZ) on the master", resized == 0, $"errno {Marshal.GetLastWin32Error()}");

// posix_spawn RETURNS the error number; it does not set errno. Reading errno here reported 0 on
// a real failure, which is how the pointer bug above stayed hidden for one run.
var spawnRc = posix_spawn(out var pid, "/bin/sh", actions, IntPtr.Zero, argv, envp);
if (spawnRc != 0)
{
    Fail("posix_spawn with dup2 file actions", $"rc {spawnRc}");
    return Report();
}

Ok("posix_spawn accepted the slave fd as the child's stdio", $"pid={pid}");

// The parent must close its copy of the slave, or the read below never sees EOF.
close(slave);

var output = new StringBuilder();
var buffer = new byte[4096];

while (true)
{
    var n = read(master, buffer, buffer.Length);
    if (n <= 0)
    {
        break;
    }

    output.Append(Encoding.UTF8.GetString(buffer, 0, (int)n));

    if (output.ToString().Contains("PTYCHECK-DONE"))
    {
        break;
    }
}

waitpid(pid, out _, 0);
close(master);

var text = output.ToString();
Console.WriteLine("--- child said ---");
Console.WriteLine(text.Trim());
Console.WriteLine("------------------");

Check("the child got a controlling terminal", text.Contains("/dev/"), text);
Check("it is a pty, not a pipe", !text.Contains("not a tty"), text);
Check("the geometry we set is what the child sees", text.Contains("44 137"), text);

return Report();

static IntPtr[] Terminated(string[] values)
{
    var pointers = new IntPtr[values.Length + 1];
    for (var i = 0; i < values.Length; i++)
    {
        pointers[i] = Marshal.StringToHGlobalAnsi(values[i]);
    }

    pointers[^1] = IntPtr.Zero;
    return pointers;
}

void Ok(string what, string detail) => Console.WriteLine($"  ok   {what} — {detail}");

void Fail(string what, string detail)
{
    failures++;
    Console.WriteLine($"  FAIL {what} — {detail}");
}

void Check(string what, bool ok, string detail)
{
    if (ok)
    {
        Ok(what, "as expected");
    }
    else
    {
        Fail(what, Trim(detail));
    }
}

static string Trim(string s) =>
    s.Length <= 160 ? s.Replace("\n", "\\n") : s[..160].Replace("\n", "\\n") + " …";

int Report()
{
    Console.WriteLine(
        failures == 0
            ? "\nTASK 2.1: openpty + posix_spawn WORKS from .NET"
            : $"\nTASK 2.1: {failures} FAILED — design D3 needs amending"
    );
    return failures == 0 ? 0 : 1;
}

// ioctl is VARIADIC — `int ioctl(int, unsigned long, ...)`. On arm64 macOS variadic arguments are
// passed on the stack, not in registers, so a plain DllImport puts the pointer where the callee
// never looks: the call returns 0 and the size is set from whatever was on the stack (measured:
// 6608x27390 instead of 44x137). __arglist is the documented way to call a C variadic — and it is
// only valid on a method of a TYPE, never a local function (CS1669), which is why this class exists.
static class Libc
{
    [DllImport("libc", SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int ioctl(int fd, ulong request, __arglist);
}

[StructLayout(LayoutKind.Sequential)]
struct Winsize
{
    public ushort Rows;
    public ushort Cols;
    public ushort XPixel;
    public ushort YPixel;
}

// Opaque to us; sized generously so the runtime's marshalling never truncates the real struct.
