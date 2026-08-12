using System.Runtime.InteropServices;
using AiOrchestrator.ServiceDefaults.Agents;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// #332 — the setup seam against real child processes. Every claim the design makes about the shell
/// was measured before it entered the design; these are the same claims, pinned so a later change
/// cannot quietly break them.
/// <para>
/// Unix-only by construction: the assertions are about <c>/bin/sh</c>'s own semantics, and asserting
/// them against <c>cmd.exe</c> would be asserting something else. The Windows path is exercised by
/// running the suite on Windows, where these return early and the shell that ships there is the one
/// under test in the functional lane. An early return rather than a skip attribute: the repository
/// has no skip facility, and adding a package for test ergonomics would be a dependency this change
/// promised not to add.
/// </para>
/// </summary>
public class LocalCheckoutSetupRunner_Should_Constraint
{
    static bool OnUnix => !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    readonly LocalCheckoutSetupRunner _runner = new();

    [Fact]
    public async Task AChainedCommandLine_Should_RunAndCarryItsStatus()
    {
        if (!OnUnix)
        {
            return;
        }

        using var directory = new TemporaryDirectory();

        var outcome = await Run("echo one && echo two && exit 3", directory.Path, out var streamed);

        // Measured 2026-08-12 and pinned here: the whole line runs, and the status survives. This
        // is why the field holds a command LINE — argv could not express `install && build`.
        outcome.ExitCode.ShouldBe(3);
        outcome.TimedOut.ShouldBeFalse();
        outcome.Succeeded.ShouldBeFalse();
        outcome.Output.ShouldContain("one");
        outcome.Output.ShouldContain("two");
        streamed.ShouldContain("one");
    }

    [Fact]
    public async Task ASucceedingLastCommand_Should_ReportSuccess_WhateverCameBefore()
    {
        if (!OnUnix)
        {
            return;
        }

        using var directory = new TemporaryDirectory();

        var outcome = await Run("false; echo after", directory.Path, out _);

        // The shell's rule, not the product's: `a; b` reports only b's status. Pinned deliberately
        // — the spec states it so that a Run which did not fail on a failing `a` reads as the
        // shell's semantics rather than as a bug in this seam.
        outcome.ExitCode.ShouldBe(0);
        outcome.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task TheCommand_Should_RunInTheDirectoryItWasGiven()
    {
        if (!OnUnix)
        {
            return;
        }

        using var directory = new TemporaryDirectory();

        var outcome = await Run("pwd && echo marker > marker.txt", directory.Path, out _);

        outcome.Succeeded.ShouldBeTrue();
        File.Exists(Path.Combine(directory.Path, "marker.txt")).ShouldBeTrue();
    }

    [Fact]
    public async Task StandardError_Should_ReachTheOutputAndTheStream()
    {
        if (!OnUnix)
        {
            return;
        }

        using var directory = new TemporaryDirectory();

        // A build's diagnostics are on stderr, so a refusal that dropped it would carry the
        // progress lines and none of the reason.
        var outcome = await Run("echo compilation-failed >&2", directory.Path, out var streamed);

        outcome.Output.ShouldContain("compilation-failed");
        streamed.ShouldContain("compilation-failed");
    }

    [Fact]
    public async Task ACommandOutlivingItsBudget_Should_BeKilledWithItsChildren()
    {
        if (!OnUnix)
        {
            return;
        }

        using var directory = new TemporaryDirectory();

        var startedAt = DateTimeOffset.UtcNow;
        var outcome = await _runner.Run(
            "sleep 30",
            directory.Path,
            TimeSpan.FromMilliseconds(600),
            _ => { }
        );
        var elapsed = DateTimeOffset.UtcNow - startedAt;

        // BR-005's own outcome, distinct from any exit code — the executor turns exactly this into
        // "the run exceeded its timeout" rather than into a setup failure.
        outcome.TimedOut.ShouldBeTrue();
        outcome.Succeeded.ShouldBeFalse();
        // Actually killed, not merely reported: an install spawns children, and the whole tree goes
        // (HeadlessProcess uses Kill(entireProcessTree: true)).
        elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task Output_Should_ArriveWhileTheCommandIsStillRunning()
    {
        if (!OnUnix)
        {
            return;
        }

        using var directory = new TemporaryDirectory();

        // UC-027's requirement at this level: a setup that hangs must be legible while it hangs,
        // so the callback cannot be a post-exit replay of a buffer.
        var firstLineAt = (DateTimeOffset?)null;
        var startedAt = DateTimeOffset.UtcNow;

        await _runner.Run(
            "echo early; sleep 2",
            directory.Path,
            TimeSpan.FromSeconds(30),
            _ => firstLineAt ??= DateTimeOffset.UtcNow
        );

        firstLineAt.ShouldNotBeNull();
        (firstLineAt.Value - startedAt).ShouldBeLessThan(
            TimeSpan.FromSeconds(2),
            "the first line must arrive before the command ends, not with it"
        );
    }

    Task<BuildingBlocks.Agents.LocalSetupOutcome> Run(
        string commandLine,
        string directory,
        out List<string> streamed
    )
    {
        var lines = new List<string>();
        streamed = lines;
        return _runner.Run(
            commandLine,
            directory,
            TimeSpan.FromSeconds(30),
            line =>
            {
                lock (lines)
                {
                    lines.Add(line);
                }
            }
        );
    }

    sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("setup-seam-").FullName;

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
