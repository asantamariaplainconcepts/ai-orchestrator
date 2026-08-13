using System.Text;
using AiOrchestrator.ServiceDefaults.Agents;
using Shouldly;
using Xunit;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// A terminal on this machine, in a Run's own checkout (#358, DEC-070).
/// <para>
/// <b>Ungated, unlike the sbx terminal tests beside it</b>, and that is the point: a host terminal needs
/// no daemon and no microVM, only a shell — so the bounds the decision turns on can be exercised
/// wherever the suite runs rather than only on a machine that happens to have sbx. DEC-070's two bounds
/// are exactly the kind of claim ADR-0001 says must be met rather than asserted: both would look correct
/// in review and fail in use.
/// </para>
/// </summary>
public sealed class LocalRunTerminal_Should_Constraint
{
    /// <summary>
    /// DEC-070 bound 1. Before this, `InteractivePty` had no working-directory parameter at all, so a
    /// host shell would have opened wherever the server process happened to be — usually the repository
    /// root, which is the operator's own folder that #331 exists to keep untouched.
    /// </summary>
    [Fact]
    public void APty_Should_StartTheShellInTheDirectoryNamed()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aio-pty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            using var pty = InteractivePty.Start(
                "/bin/bash",
                ["--noprofile", "--norc", "-i"],
                new Dictionary<string, string>
                {
                    ["PATH"] = "/usr/local/bin:/usr/bin:/bin",
                    ["HOME"] = directory,
                    ["TERM"] = "dumb",
                },
                columns: 120,
                rows: 30,
                workingDirectory: directory,
                inheritEnvironment: false
            );

            pty.Write(Encoding.UTF8.GetBytes("pwd\n"));

            // `realpath` because macOS reports /var/folders/... for a /private/var/folders/... temp path
            // and the shell prints whichever the kernel gives it. Comparing the leaf avoids asserting
            // against a platform's symlink policy, which is not what this test is about.
            var leaf = Path.GetFileName(directory);
            ReadUntil(pty, leaf).ShouldContain(leaf);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// DEC-070 bound 2, and the reason the decision was worth writing. `posix_spawn` takes the child's
    /// whole environment, and the sandbox path deliberately <b>inherits and overlays</b> because the sbx
    /// CLI panics without <c>$HOME</c>. Behind a microVM that is harmless — nothing crosses the boundary.
    /// On the host there is no boundary, so the same code would hand whoever is typing everything the
    /// habitat resolved into this process, a Connector's credential among it.
    /// <para>
    /// Asserted by putting a marker in <i>this</i> process and requiring the child not to see it.
    /// </para>
    /// </summary>
    [Fact]
    public void APty_Should_NotHandTheChildThisProcessesEnvironment()
    {
        var marker = $"AIO_LEAK_PROBE_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(marker, "leaked");

        var directory = Path.Combine(Path.GetTempPath(), $"aio-pty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            using var pty = InteractivePty.Start(
                "/bin/bash",
                ["--noprofile", "--norc", "-i"],
                new Dictionary<string, string>
                {
                    ["PATH"] = "/usr/local/bin:/usr/bin:/bin",
                    ["HOME"] = directory,
                    ["TERM"] = "dumb",
                },
                columns: 120,
                rows: 30,
                workingDirectory: directory,
                inheritEnvironment: false
            );

            // The sentinel is what makes the absence readable: without it, a shell that printed nothing
            // for any other reason would look like a pass.
            //
            // `probe--end` is chosen so it CANNOT appear in the command the tty echoes back. A terminal
            // echoes what you type, so waiting for a substring that occurs in the command itself returns
            // before the shell has answered — the first version of this test did exactly that and
            // "passed" on the echo. With the variable unset the shell prints `probe--end`; the echoed
            // command reads `probe-$AIO_LEAK_PROBE_…-end`, which does not match.
            pty.Write(Encoding.UTF8.GetBytes($"echo \"probe-${marker}-end\"\n"));

            var seen = ReadUntil(pty, "probe--end");
            seen.ShouldContain(
                "probe--end",
                customMessage: "the child saw this process's environment — a host terminal must not "
                    + "inherit it (DEC-070)"
            );
            seen.ShouldNotContain("leaked");
        }
        finally
        {
            Environment.SetEnvironmentVariable(marker, null);
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// A directory that is not there is refused, rather than the shell quietly starting somewhere else.
    /// The failure mode this prevents is the one worth naming: a terminal that opens in the wrong place
    /// looks like it worked.
    /// </summary>
    [Fact]
    public void APty_Should_RefuseADirectoryThatIsNotThere()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"aio-absent-{Guid.NewGuid():N}");

        Should
            .Throw<AgentProcessHostException>(() =>
                InteractivePty.Start(
                    "/bin/bash",
                    ["--noprofile", "--norc", "-i"],
                    new Dictionary<string, string> { ["PATH"] = "/bin" },
                    columns: 80,
                    rows: 24,
                    workingDirectory: missing,
                    inheritEnvironment: false
                )
            )
            .Message.ShouldContain(missing);
    }

    [Fact]
    public void TheLedger_Should_AnswerBothDirectionsWhileTheRunExecutes()
    {
        var host = new RunCheckoutHost();
        var runId = Guid.NewGuid();
        var checkout = "/tmp/aio-checkout-abc";

        host.Hosted.ShouldBeTrue();
        host.NameFor(runId).ShouldBeNull("nothing is addressable before a Run occupies a checkout");

        host.Created(runId, checkout);

        host.NameFor(runId).ShouldBe(checkout);
        host.RunUsing(checkout).ShouldBe(runId);
        host.Targets().ShouldHaveSingleItem().RunId.ShouldBe(runId);

        host.Gone(runId);

        host.NameFor(runId).ShouldBeNull("a finished Run has no terminal");
        host.RunUsing(checkout).ShouldBeNull();
        host.Targets().ShouldBeEmpty();
        Should.NotThrow(() => host.Gone(runId));
    }

    /// <summary>
    /// The pairing is published by the one component that knows both halves, and — the part worth a test
    /// — it is <b>removed when the agent exits</b>. A ledger that only ever grew would leave a terminal
    /// addressable for a Run that had finished, which is the lie the in-memory design exists to avoid.
    /// </summary>
    [Fact]
    public async Task TheProcessHost_Should_PublishItsCheckoutOnlyWhileTheAgentRuns()
    {
        var checkouts = new RunCheckoutHost();
        var host = new LocalAgentProcessHost(checkouts);
        var runId = Guid.NewGuid();
        var observed = new List<string?>();

        await host.Run(
            "/bin/sh",
            ["-c", "exit 0"],
            Path.GetTempPath(),
            new Dictionary<string, string>(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None,
            onOutput: _ => observed.Add(checkouts.NameFor(runId)),
            runId: runId
        );

        checkouts
            .NameFor(runId)
            .ShouldBeNull("the checkout stops being addressable when the agent exits");
    }

    /// <summary>
    /// Reads until <paramref name="expected"/> appears or the deadline passes. Derived rather than
    /// guessed: it returns as soon as the shell has said the thing, so the wall-clock budget bounds only
    /// failure — the shape #329 asks for, and the opposite of the fixed ten-second wait #327 removed.
    /// </summary>
    static string ReadUntil(InteractivePty pty, string expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        var seen = new StringBuilder();
        var buffer = new byte[4096];

        while (DateTime.UtcNow < deadline)
        {
            var read = pty.Read(buffer);
            if (read == 0)
            {
                break;
            }

            seen.Append(Encoding.UTF8.GetString(buffer, 0, read));
            if (seen.ToString().Contains(expected, StringComparison.Ordinal))
            {
                return seen.ToString();
            }
        }

        return seen.ToString();
    }
}
