using System.Diagnostics;
using AiOrchestrator.ServiceDefaults.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// The startup sweep for Local-Run checkouts (#331, design D5) — the analogue of the sandbox
/// reaper, and against real git, because what is being asserted is what git does to a branch when
/// its worktree goes away. A fake would prove nothing about that.
/// </summary>
public class LocalCheckoutReaper_Should_Constraint : IDisposable
{
    readonly string _repository = Directory.CreateTempSubdirectory("reaper-repo-").FullName;

    /// <summary>
    /// This test's own checkout root. The machine's real temp is shared with every other test
    /// project running at the same time, and a sweep there would reap their live checkouts — the
    /// cross-process hazard the roster documents, which two test projects would otherwise
    /// reproduce as flakiness nobody could explain.
    /// </summary>
    readonly string _root = Directory.CreateTempSubdirectory("reaper-root-").FullName;

    readonly List<string> _checkouts = [];

    public LocalCheckoutReaper_Should_Constraint()
    {
        Git(_repository, "init", "--initial-branch=main");
        File.WriteAllText(Path.Combine(_repository, "readme.md"), "hello");
        Git(_repository, "add", "--all");
        Git(
            _repository,
            "-c",
            "user.name=Owner",
            "-c",
            "user.email=owner@example.invalid",
            "commit",
            "-m",
            "seed"
        );
    }

    [Theory]
    // The one prefix this host creates and reaps.
    [InlineData("aio-checkout-3f2a9c1d4e5b6a7c8d9e0f1a", true)]
    // Sibling `aio-` names on the machine are host paths of other kinds, not checkouts. A wildcard
    // here would delete a sandbox's staging directory mid-Run.
    [InlineData("aio-carry-1234", false)]
    [InlineData("aio-workspace-1234", false)]
    [InlineData("aio-run-1234", false)]
    [InlineData("aio-", false)]
    // Anchored: a name that merely contains ours is not ours.
    [InlineData("not-aio-checkout-1234", false)]
    [InlineData("", false)]
    public void ThePredicate_Should_ClaimOnlyTheHostsOwnCheckouts(string name, bool claimed) =>
        LocalCheckoutRoster.Claims(name).ShouldBe(claimed);

    [Fact]
    public void ThePredicate_Should_TreatAnAbsentNameAsNotOurs() =>
        LocalCheckoutRoster.Claims(null).ShouldBeFalse();

    [Fact]
    public async Task TheSweep_Should_RemoveACheckoutADeadProcessLeftBehind()
    {
        var abandoned = Abandon("ai/1-orphan");

        Directory.Exists(abandoned).ShouldBeTrue("arrange failed: no checkout to reap");

        await Sweep();

        Directory.Exists(abandoned).ShouldBeFalse();
        // Pruned, not merely deleted behind git's back — a stale record makes the next
        // `worktree add` for that branch fail for a reason nobody can act on.
        Git(_repository, "worktree", "list").ShouldNotContain(abandoned);
    }

    [Fact]
    public async Task TheSweep_Should_NeverDestroyARunsOutput()
    {
        // Spec scenario "reaping never destroys a Run's output". The branch is what the Run
        // produced; the checkout is only where it typed. Reaping the second must never cost the
        // first, and this is the assertion that keeps a future `branch -D` out of the sweep.
        var abandoned = Abandon("ai/1-carries-a-commit");
        File.WriteAllText(Path.Combine(abandoned, "work.txt"), "what the agent did");
        Git(abandoned, "add", "--all");
        Git(
            abandoned,
            "-c",
            "user.name=AI Orchestrator",
            "-c",
            "user.email=agent@ai-orchestrator.invalid",
            "commit",
            "-m",
            "ai: work"
        );
        var commit = Git(abandoned, "rev-parse", "HEAD").Trim();

        await Sweep();

        Directory.Exists(abandoned).ShouldBeFalse();
        Git(_repository, "branch", "--list", "ai/1-carries-a-commit")
            .ShouldContain("ai/1-carries-a-commit");
        // …carrying the commit, not merely existing as a name.
        Git(_repository, "rev-parse", "ai/1-carries-a-commit").Trim().ShouldBe(commit);
    }

    [Fact]
    public async Task TheSweep_Should_LeaveALiveRunsCheckoutAlone()
    {
        var live = Abandon("ai/1-still-running");
        LocalCheckoutRoster.Occupy(live);

        try
        {
            await Sweep();

            Directory.Exists(live).ShouldBeTrue("the sweep reaped a checkout a Run was using");
        }
        finally
        {
            LocalCheckoutRoster.Release(live);
        }
    }

    [Fact]
    public async Task TheSweep_Should_RemoveADirectoryInOurNamespaceThatIsNoLongerAWorktree()
    {
        // The half-removed crash case: the directory survives with no usable `.git` pointer, so
        // git cannot be asked about it. It is still in our namespace and still waste.
        var stranded = LocalCheckoutRoster.NewCheckout(_root);
        Directory.CreateDirectory(stranded);
        _checkouts.Add(stranded);
        File.WriteAllText(Path.Combine(stranded, "leftover.txt"), "x");

        await Sweep();

        Directory.Exists(stranded).ShouldBeFalse();
    }

    /// <summary>A checkout created the way a Run creates one, then forgotten the way a crash forgets it.</summary>
    string Abandon(string branch)
    {
        var checkout = LocalCheckoutRoster.NewCheckout(_root);
        _checkouts.Add(checkout);
        Git(_repository, "worktree", "add", checkout, "-b", branch);
        return checkout;
    }

    Task Sweep() =>
        new LocalCheckoutReaper(NullLogger<LocalCheckoutReaper>.Instance, _root).StartAsync(
            CancellationToken.None
        );

    public void Dispose()
    {
        foreach (var directory in _checkouts.Append(_root).Append(_repository))
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        GC.SuppressFinalize(this);
    }

    static string Git(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return stdout;
    }
}
