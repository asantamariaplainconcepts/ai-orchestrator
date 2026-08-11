using System.Text;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.ServiceDefaults.Agents;
using AiOrchestrator.ServiceDefaults.Agents.Sbx;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// The machine-wide sandboxes surface against the real sbx CLI (#311). Gated for ADR-0020's reason: this
/// slice rests on two facts about the CLI that a stand-in would let us assert backwards — that
/// <c>ls --json</c> reports a status, and that <c>exec</c> on a stopped sandbox <b>starts</b> it.
/// <para>
/// Run with <c>SBX_TERMINAL_TESTS=1</c> on a machine with the sbx daemon running. Skipped otherwise: it
/// creates real microVMs, and CI has no daemon.
/// </para>
/// <para>
/// Every sandbox here is named <c>aio-run-*</c> deliberately — the namespace this host claims — because a
/// name outside it is precisely what the code under test refuses. It is also what the startup sweep reaps,
/// so a crashed test leaks nothing that the next process will not clean up.
/// </para>
/// </summary>
public sealed class RealMachineSandboxes_Should_Constraint
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
    public async Task TheListing_Should_HoldThisProductsSandboxesAndNotAnybodyElses()
    {
        if (!Enabled)
        {
            return;
        }

        var mine = Name();
        var foreign = $"notaio-{Guid.NewGuid():N}"[..18];

        Sbx("run", "-d", "--name", mine, "shell", Path.GetTempPath());
        Sbx("run", "-d", "--name", foreign, "shell", Path.GetTempPath());

        try
        {
            var listed = await Host().List(CancellationToken.None);

            listed.Select(sandbox => sandbox.Name).ShouldContain(mine);

            // Criterion 2 against a sandbox that really exists on the machine — the case a fake cannot
            // prove, because a fake never had to actually be excluded from anything.
            listed.Select(sandbox => sandbox.Name).ShouldNotContain(foreign);

            // The status is the CLI's own word, and the surface needs it to warn before starting one.
            listed.Single(sandbox => sandbox.Name == mine).Status.ShouldNotBeNullOrWhiteSpace();
        }
        finally
        {
            Sbx("rm", "--force", mine);
            Sbx("rm", "--force", foreign);
        }
    }

    [Fact]
    public async Task ANameOutsideTheClaimedNamespace_Should_NeverReachTheCli()
    {
        if (!Enabled)
        {
            return;
        }

        var foreign = $"notaio-{Guid.NewGuid():N}"[..18];
        Sbx("run", "-d", "--name", foreign, "shell", Path.GetTempPath());

        try
        {
            // It exists and is running, so the only thing refusing it is the namespace bound.
            (await Host().Open(foreign, 80, 24, CancellationToken.None)).ShouldBeNull();

            // And a name that exists nowhere is refused the same way, which is what stops the refusal
            // being a way to discover what is on the machine.
            (
                await Host().Open($"aio-run-{Guid.NewGuid():N}", 80, 24, CancellationToken.None)
            ).ShouldBeNull();
        }
        finally
        {
            Sbx("rm", "--force", foreign);
        }
    }

    [Fact]
    public async Task AListedSandbox_Should_GiveARealTerminalAndTakeAnInterruptAsASignal()
    {
        if (!Enabled)
        {
            return;
        }

        var sandbox = Name();
        Sbx("run", "-d", "--name", sandbox, "shell", Path.GetTempPath());

        try
        {
            using var terminal = await Host().Open(sandbox, 80, 24, CancellationToken.None);
            terminal.ShouldNotBeNull();

            // A long sleep, then Ctrl-C. On a pipe the 0x03 would be a byte the shell echoes; on a
            // terminal it is SIGINT, and the marker only prints if the sleep was cut short.
            terminal.Write(Encoding.UTF8.GetBytes("sleep 300\r"));
            Thread.Sleep(TimeSpan.FromSeconds(2));
            terminal.Write([0x03]);
            terminal.Write(Encoding.UTF8.GetBytes("echo INTERRUPTED\r"));

            Drain(terminal, until: "INTERRUPTED").ShouldContain("INTERRUPTED");
        }
        finally
        {
            Sbx("rm", "--force", sandbox);
        }
    }

    [Fact]
    public async Task AStoppedSandbox_Should_BeStartedByBeingEntered()
    {
        if (!Enabled)
        {
            return;
        }

        // The fact design decision D5 rests on, pinned rather than trusted: `sbx exec` on a stopped
        // sandbox starts it. If a CLI upgrade ever changes that, the surface's warning becomes a lie and
        // this is what says so.
        var sandbox = Name();
        Sbx("run", "-d", "--name", sandbox, "shell", Path.GetTempPath());
        Sbx("stop", sandbox);

        try
        {
            var host = Host();

            (await host.List(CancellationToken.None))
                .Single(entry => entry.Name == sandbox)
                .Status.ShouldNotBe("running");

            using var terminal = await host.Open(sandbox, 80, 24, CancellationToken.None);
            terminal.ShouldNotBeNull();

            terminal.Write(Encoding.UTF8.GetBytes("echo STARTED\r"));
            Drain(terminal, until: "STARTED").ShouldContain("STARTED");

            (await host.List(CancellationToken.None))
                .Single(entry => entry.Name == sandbox)
                .Status.ShouldBe("running");
        }
        finally
        {
            Sbx("rm", "--force", sandbox);
        }
    }

    [Fact]
    public async Task ASandboxDisposedUnderneathATerminal_Should_EndTheShell()
    {
        if (!Enabled)
        {
            return;
        }

        // Criterion 6: the reader is told the sandbox is gone rather than left on a dead terminal. A read
        // returning 0 is how the hub learns to send "ended", so 0 is the thing to assert.
        var sandbox = Name();
        Sbx("run", "-d", "--name", sandbox, "shell", Path.GetTempPath());

        using var terminal = await Host().Open(sandbox, 80, 24, CancellationToken.None);
        terminal.ShouldNotBeNull();

        terminal.Write(Encoding.UTF8.GetBytes("echo READY\r"));
        Drain(terminal, until: "READY").ShouldContain("READY");

        Sbx("rm", "--force", sandbox);

        var buffer = new byte[4096];
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        var ended = false;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (terminal.Read(buffer) == 0)
            {
                ended = true;
                break;
            }
        }

        ended.ShouldBeTrue();
    }

    /// <summary>A claimed name, short enough for sbx and unmistakably this host's.</summary>
    static string Name() => $"aio-run-{Guid.NewGuid():N}"[..20];

    static IRunTerminalHost Host() =>
        new SbxRunTerminalHost(
            new SbxSandboxOptions
            {
                CommandPath = SbxPath,
                Memory = "1g",
                InjectedSecrets = [],
                SessionFiles = [],
            },
            new RunSandboxHost(),
            NullLogger<SbxRunTerminalHost>.Instance
        );

    static string Drain(IRunTerminal terminal, string until)
    {
        var text = new StringBuilder();
        var buffer = new byte[4096];
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var read = terminal.Read(buffer);
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
