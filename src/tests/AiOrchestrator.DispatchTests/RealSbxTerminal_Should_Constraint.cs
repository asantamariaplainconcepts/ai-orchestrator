using System.Text;
using AiOrchestrator.ServiceDefaults.Agents;
using Shouldly;
using Xunit;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// The terminal seam against the real sbx CLI (#304). Gated, and gated for ADR-0020's reason: a
/// launcher is unverified until it has met its real CLI, and every fact this asserts was measured
/// rather than assumed — `sbx exec -it` refuses a plain pipe, so a stand-in that accepted one would
/// have proved the opposite of the truth.
/// <para>
/// Run with <c>SBX_TERMINAL_TESTS=1</c> on a machine with the sbx daemon running. Skipped otherwise:
/// it creates a real microVM, and CI has no daemon.
/// </para>
/// </summary>
public sealed class RealSbxTerminal_Should_Constraint
{
    static bool Enabled =>
        Environment.GetEnvironmentVariable("SBX_TERMINAL_TESTS") is { Length: > 0 };

    static string SbxPath =>
        Environment.GetEnvironmentVariable("SBX_PATH")
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local/bin/sbx"
        );

    [Fact]
    public void APty_Should_GiveASandboxedShellARealTerminal()
    {
        if (!Enabled)
        {
            return;
        }

        var sandbox = $"aio-term-{Guid.NewGuid():N}"[..20];
        Sbx("run", "-d", "--name", sandbox, "shell", Path.GetTempPath());

        try
        {
            using var pty = InteractivePty.Start(
                SbxPath,
                ["exec", "-it", sandbox, "sh", "-c", "tty; stty size; echo READY"],
                new Dictionary<string, string> { ["TERM"] = "xterm-256color" },
                columns: 137,
                rows: 44
            );

            var output = Drain(pty, until: "READY");

            // The three facts a pipe cannot produce. `not a tty` is what `sbx exec -i` answers, and
            // seeing it here would mean the terminal was never handed over.
            output.ShouldContain("/dev/");
            output.ShouldNotContain("not a tty");
            output.ShouldContain("44 137");
        }
        finally
        {
            Sbx("rm", "--force", sandbox);
        }
    }

    [Fact]
    public void APty_Should_DeliverAnInterruptAsASignal()
    {
        if (!Enabled)
        {
            return;
        }

        var sandbox = $"aio-term-{Guid.NewGuid():N}"[..20];
        Sbx("run", "-d", "--name", sandbox, "shell", Path.GetTempPath());

        try
        {
            using var pty = InteractivePty.Start(
                SbxPath,
                ["exec", "-it", sandbox, "sh"],
                new Dictionary<string, string> { ["TERM"] = "xterm-256color" },
                columns: 80,
                rows: 24
            );

            // A long sleep, then Ctrl-C. On a pipe the 0x03 would be a byte the shell echoes; on a
            // terminal it is SIGINT, and the marker after it only prints if the sleep was cut short.
            pty.Write(Encoding.UTF8.GetBytes("sleep 300\r"));
            Thread.Sleep(TimeSpan.FromSeconds(2));
            pty.Write([0x03]);
            pty.Write(Encoding.UTF8.GetBytes("echo INTERRUPTED\r"));

            Drain(pty, until: "INTERRUPTED").ShouldContain("INTERRUPTED");
        }
        finally
        {
            Sbx("rm", "--force", sandbox);
        }
    }

    static string Drain(InteractivePty pty, string until)
    {
        var text = new StringBuilder();
        var buffer = new byte[4096];
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var read = pty.Read(buffer);
            if (read == 0)
            {
                break;
            }

            text.Append(Encoding.UTF8.GetString(buffer, 0, read));

            if (text.ToString().Contains(until, StringComparison.Ordinal))
            {
                break;
            }
        }

        return text.ToString();
    }

    static void Sbx(params string[] arguments)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(SbxPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(psi)!;
        process.WaitForExit(TimeSpan.FromMinutes(2));
    }
}
