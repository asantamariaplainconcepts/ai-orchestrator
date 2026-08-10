using AiOrchestrator.ServiceDefaults.Agents;
using AiOrchestrator.ServiceDefaults.Agents.Sbx;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// The enumeration cache's key (#291, design D3) — the one correctness claim in this change that
/// reads like a performance detail.
/// <para>
/// <c>CliAnswers</c> caches on a stated justification: its answer is a property of the template
/// image, which does not move between two probes. A model list does not inherit that. It is a
/// property of the image <b>and of the session the sandbox carries in</b> — the `github-copilot/*`
/// entries observed inside a sandbox exist because #288 copied a seat. So a developer who
/// re-authenticates must stop being served the models of the seat they left.
/// </para>
/// <para>
/// The sbx CLI is stood in for by a script that COUNTS how many times it was asked, which is what
/// makes both halves able to fail: a host that never cached would fail the first assertion, and a
/// host keyed only on the command would fail the second.
/// </para>
/// </summary>
public class ModelListCache_Should_Constraint
{
    [Fact]
    public async Task AskingTwice_Should_CostOneSandbox()
    {
        var (host, session, counter) = CarryingHost();

        var first = await host.ListModels("opencode", ["models"], CancellationToken.None);
        var second = await host.ListModels("opencode", ["models"], CancellationToken.None);

        first.ShouldBe(["opencode/one", "github-copilot/two"]);
        second.ShouldBe(first);
        // Creating a sandbox costs seconds, so a second ask that reached the CLI would be the bug
        // the cache exists to prevent.
        Asks(counter).ShouldBe(1);

        session.Delete();
    }

    [Fact]
    public async Task ReAuthenticating_Should_NotBeServedTheOldSeatsModels()
    {
        var (host, session, counter) = CarryingHost();

        await host.ListModels("opencode", ["models"], CancellationToken.None);

        // What re-authenticating looks like from outside: the carried credential file changes.
        // A cache keyed on the command alone cannot see this and would keep serving the models
        // of a seat this machine is no longer signed into.
        await File.WriteAllTextAsync(session.FullName, "a different seat entirely");

        await host.ListModels("opencode", ["models"], CancellationToken.None);

        Asks(counter).ShouldBe(2);

        session.Delete();
    }

    [Fact]
    public async Task AMachineThatCouldNotBeAsked_Should_NotCacheItsSilence()
    {
        // "Could not ask" is a state of this moment, not a property of the machine. Caching it
        // would keep every chooser empty for the whole probe interval after the daemon came back.
        // An unreachable machine reaches THIS path as a sandbox that cannot be created — the
        // daemon check belongs to Run's preconditions and is never on the enumeration path.
        var (host, session, counter) = CarryingHost(daemonRunning: false);

        (await host.ListModels("opencode", ["models"], CancellationToken.None)).ShouldBeNull();
        (await host.ListModels("opencode", ["models"], CancellationToken.None)).ShouldBeNull();

        // Neither attempt got as far as asking, and neither was remembered as an answer: the
        // second try is a fresh attempt rather than a cached silence.
        Asks(counter).ShouldBe(0);

        session.Delete();
    }

    // ---- Stand-ins ----

    /// <summary>
    /// A sandbox host carrying one session file, whose sbx is a script that records every
    /// <c>exec … models</c> it is asked to perform.
    /// </summary>
    static (SbxAgentProcessHost Host, FileInfo Session, string Counter) CarryingHost(
        bool daemonRunning = true
    )
    {
        var directory = Directory.CreateTempSubdirectory("model-cache-").FullName;
        var counter = Path.Combine(directory, "asks");
        var script = Path.Combine(directory, "sbx.sh");

        // The session file lives under the home directory, because that is where the host looks
        // for what it carries. Named per test run so concurrent tests cannot collide.
        var relative = Path.Combine(".aio-test-sessions", $"{Guid.NewGuid():N}.json");
        var session = new FileInfo(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), relative)
        );
        session.Directory!.Create();
        File.WriteAllText(session.FullName, "the seat this machine started on");

        File.WriteAllText(
            script,
            $"""
            #!/bin/sh
            case "$1 $2" in
              "daemon status") {(
                daemonRunning
                    ? "echo 'Status: running'; exit 0"
                    : "echo 'not reachable' >&2; exit 1"
            )} ;;
              "secret ls") echo 'github'; exit 0 ;;
              "run -d") {(
                daemonRunning
                    ? "echo 'created'; exit 0"
                    : "echo 'the sandbox host is not reachable' >&2; exit 1"
            )} ;;
              "cp "*) exit 0 ;;
              "rm "*) exit 0 ;;
            esac
            case "$*" in
              *models*)
                echo x >> "{counter}"
                printf 'opencode/one\ngithub-copilot/two\n'
                exit 0 ;;
            esac
            exit 0

            """
        );

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                script,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        }

        var host = new SbxAgentProcessHost(
            new SbxSandboxOptions
            {
                CommandPath = script,
                Memory = "1g",
                InjectedSecrets = ["github"],
                SessionFiles = [relative],
            },
            new RunPreviewHost(),
            NullLogger<SbxAgentProcessHost>.Instance
        );

        return (host, session, counter);
    }

    static int Asks(string counter) => File.Exists(counter) ? File.ReadAllLines(counter).Length : 0;
}
