using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.ServiceDefaults.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// The Azure sandbox host (#296). The `aca` CLI is stood in for by a shell script — this
/// repository's idiom for exercising a process contract without the real binary — and the script
/// **records every invocation**, so "did not disable auto-suspend" and "did not apply an egress
/// policy" are assertions rather than assumptions.
/// <para>
/// The load-bearing one is the poll loop: the real CLI's `exec` fails between 50 and 60 seconds
/// (measured, three attempts) while a Run may last thirty minutes, so a stand-in that returned
/// instantly would prove nothing. The script here makes the agent finish only after several polls,
/// which is what a Run outliving a single call looks like from this side.
/// </para>
/// </summary>
public class AcaSandbox_Should_Constraint
{
    const string SandboxId = "11111111-2222-3333-4444-555555555555";

    [Fact]
    public async Task ALongRun_Should_CompleteAndStreamDespiteTheExecCeiling()
    {
        // A Run that no single exec could hold. The stand-in only writes its exit file on the
        // third poll, so an implementation that ran one exec and returned would never see it.
        var (host, calls) = Host(finishAfterPolls: 3);
        var streamed = new List<string>();

        var outcome = await host.Run(
            "opencode",
            ["run", "hello"],
            Workspace(),
            new Dictionary<string, string>(),
            TimeSpan.FromMinutes(5),
            CancellationToken.None,
            streamed.Add
        );

        outcome.TimedOut.ShouldBeFalse();
        outcome.ExitCode.ShouldBe(0);

        // Output arrived while it worked (UC-027), not in one lump at the end.
        streamed.ShouldContain("working 1");
        streamed.ShouldContain("working 3");

        // And it really did poll rather than block on one call.
        Invocations(calls, "exec").Count.ShouldBeGreaterThan(3);
    }

    [Fact]
    public async Task EveryRun_Should_TurnOffTheAutoSuspendThePlatformTurnsOn()
    {
        // The platform suspends on outside-idleness at 600 s — measured going Stopped at t+41 s
        // with a 60 s timeout while a process wrote inside every second. An agent that thinks for
        // ten minutes would be suspended mid-thought, so this is not optional.
        var (host, calls) = Host(finishAfterPolls: 1);

        await host.Run(
            "opencode",
            [],
            Workspace(),
            new Dictionary<string, string>(),
            TimeSpan.FromMinutes(1),
            CancellationToken.None
        );

        Invocations(calls, "lifecycle").ShouldContain(line => line.Contains("disable"));
    }

    [Fact]
    public async Task EveryRun_Should_DenyEgressByDefaultBecauseThePlatformDoesNot()
    {
        // Measured: a sandbox created with no policy reached example.com and pypi.org with 200s
        // while `egress show` reported none configured, whatever the documentation says.
        var (host, calls) = Host(finishAfterPolls: 1);

        await host.Run(
            "opencode",
            [],
            Workspace(),
            new Dictionary<string, string>(),
            TimeSpan.FromMinutes(1),
            CancellationToken.None
        );

        var egress = Invocations(calls, "egress").ShouldHaveSingleItem();
        egress.ShouldContain("--default Deny");
        egress.ShouldContain("github.com:Allow");
    }

    [Fact]
    public async Task EachProject_Should_GetItsOwnGroup()
    {
        // #244 promises a Run bills as its own Project, and this platform scopes credentials to
        // the group — so a shared group would break that promise silently.
        var project = Guid.Parse("99999999-8888-7777-6666-555555555555");
        var (host, calls) = Host(finishAfterPolls: 1, group: "aio-{project}");

        await host.Run(
            "opencode",
            [],
            Workspace(),
            new Dictionary<string, string>(),
            TimeSpan.FromMinutes(1),
            CancellationToken.None,
            onOutput: null,
            preview: null,
            projectId: project
        );

        Invocations(calls, "create")
            .ShouldHaveSingleItem()
            .ShouldContain($"--group aio-{project:N}");
    }

    [Fact]
    public async Task AHostHandedCredentialValues_Should_RefuseRatherThanCarryThemIn()
    {
        // The same assertion the sbx host makes: a future caller that forgets
        // AgentCredentialEnvironment must not smuggle a value past a boundary built to exclude it.
        var (host, _) = Host(finishAfterPolls: 1);

        var refusal = await Should.ThrowAsync<AgentProcessHostException>(() =>
            host.Run(
                "opencode",
                [],
                Workspace(),
                new Dictionary<string, string> { ["ANTHROPIC_API_KEY"] = "secret" },
                TimeSpan.FromMinutes(1),
                CancellationToken.None
            )
        );

        refusal.Message.ShouldContain("defeat the boundary");
    }

    [Fact]
    public async Task ARunThatEnds_Should_LeaveNoSandboxBehind()
    {
        // An abandoned sandbox is the leak, and on this substrate it also costs money.
        var (host, calls) = Host(finishAfterPolls: 2);

        await host.Run(
            "opencode",
            [],
            Workspace(),
            new Dictionary<string, string>(),
            TimeSpan.FromMinutes(1),
            CancellationToken.None
        );

        Invocations(calls, "delete").ShouldHaveSingleItem().ShouldContain(SandboxId);
    }

    [Fact]
    public async Task ARunThatOverrunsItsPhase_Should_TimeOutAndStillBeCleanedUp()
    {
        // BR-005 is enforced by this host, not by the platform: a sandbox would hold a runaway
        // agent indefinitely, and nothing retries afterwards (BR-004).
        var (host, calls) = Host(finishAfterPolls: 1000);

        var outcome = await host.Run(
            "opencode",
            [],
            Workspace(),
            new Dictionary<string, string>(),
            TimeSpan.FromMilliseconds(200),
            CancellationToken.None
        );

        outcome.TimedOut.ShouldBeTrue();
        Invocations(calls, "delete").ShouldHaveSingleItem();
    }

    [Fact]
    public async Task WhatTheAgentWasRefused_Should_ReachTheRunsOwnOutput()
    {
        // A deny-default policy is half a security story until somebody can see what it denied
        // (task 2.3). The platform keeps the log per sandbox, which means it has to be asked for
        // before the sandbox is deleted — so this also pins the ordering.
        var (host, calls) = Host(finishAfterPolls: 1);
        var streamed = new List<string>();

        await host.Run(
            "opencode",
            [],
            Workspace(),
            new Dictionary<string, string>(),
            TimeSpan.FromMinutes(1),
            CancellationToken.None,
            streamed.Add
        );

        // The denials, named — and the allowed request not dressed up as one.
        streamed.ShouldContain(line => line.Contains("2 outbound request(s) were denied"));
        streamed.ShouldContain(line => line.Contains("pypi.org"));
        streamed.ShouldContain(line => line.Contains("api.openai.com"));
        streamed.ShouldNotContain(line => line.Contains("[egress]") && line.Contains("github.com"));

        // Asked while there was still a sandbox to ask.
        var ledger = File.ReadAllLines(calls);
        Array
            .FindIndex(ledger, line => line.Contains("decisions"))
            .ShouldBeLessThan(Array.FindIndex(ledger, line => line.Contains(" delete ")));
    }

    [Fact]
    public async Task AnUnreadableDecisionLog_Should_SayNothingIsRecordedRatherThanFailTheRun()
    {
        // The work is finished by the time this is asked. A Run marked failed because an audit
        // query did not answer would be the tail wagging the dog — but silence would let a habitat
        // believe nothing was denied when in truth nothing was read.
        var (host, _) = Host(finishAfterPolls: 1, decisionsExitCode: 4);
        var streamed = new List<string>();

        var outcome = await host.Run(
            "opencode",
            [],
            Workspace(),
            new Dictionary<string, string>(),
            TimeSpan.FromMinutes(1),
            CancellationToken.None,
            streamed.Add
        );

        outcome.ExitCode.ShouldBe(0);
        streamed.ShouldContain(line => line.Contains("could not be read"));
    }

    // ---- Stand-ins ----

    /// <summary>
    /// A real directory, because <c>SendWorkspace</c> really packs one: since the exercise
    /// against Azure found that the platform has no recursive copy, the host tars the workspace
    /// on this machine before sending it, and a path that does not exist fails honestly.
    /// </summary>
    static string Workspace()
    {
        var directory = Directory.CreateTempSubdirectory("aca-workspace-").FullName;
        File.WriteAllText(Path.Combine(directory, "file.txt"), "workspace");
        return directory;
    }

    static IReadOnlyList<string> Invocations(string ledger, string verb) =>
        [
            .. (File.Exists(ledger) ? File.ReadAllLines(ledger) : []).Where(line =>
                line.Contains($" {verb} ", StringComparison.Ordinal)
            ),
        ];

    /// <summary>
    /// A host whose CLI is a script that records what it was asked and lets the agent "finish"
    /// only after <paramref name="finishAfterPolls"/> reads of its log — so a Run that outlives a
    /// single call is what the test actually exercises.
    /// </summary>
    static (AcaAgentProcessHost Host, string Ledger) Host(
        int finishAfterPolls,
        string group = "aio-shared",
        int decisionsExitCode = 0
    )
    {
        var directory = Directory.CreateTempSubdirectory("aca-stub-").FullName;
        var ledger = Path.Combine(directory, "calls.log");
        var script = Path.Combine(directory, "aca.sh");
        var polls = Path.Combine(directory, "polls");
        var decisions = Path.Combine(directory, "decisions.json");

        // The real CLI's answer, measured against Azure on 2026-08-09 — kept verbatim rather than
        // paraphrased, because the first version of this fixture invented a table and the code
        // that read it therefore reported nothing when it met the real thing (ADR-0016).
        File.WriteAllText(
            decisions,
            """
            {
              "networkEgress": {
                "allowed": [
                  { "timestamp": "2026-08-09T10:00:04Z", "host": "github.com",
                    "method": "GET", "path": "/acme/portal.git/info/refs", "scheme": "https" }
                ],
                "denied": [
                  { "timestamp": "2026-08-09T10:00:01Z", "host": "pypi.org",
                    "method": "GET", "path": "/simple/requests/", "scheme": "https" },
                  { "timestamp": "2026-08-09T10:00:09Z", "host": "api.openai.com",
                    "method": "POST", "path": "/v1/chat/completions", "scheme": "https" }
                ]
              }
            }
            """
        );

        File.WriteAllText(
            script,
            $"""
            #!/bin/sh
            echo " $* " >> "{ledger}"
            case "$2" in
              create) echo '{SandboxId}'; exit 0 ;;
            esac
            # Any exec whose command reads the log is a poll; count them and answer accordingly.
            case "$*" in
              *"egress decisions"*)
                if [ "{decisionsExitCode}" -ne 0 ]; then exit {decisionsExitCode}; fi
                cat "{decisions}"
                exit 0 ;;
              *".exit"*)
                n=$(cat "{polls}" 2>/dev/null || echo 0)
                if [ "$n" -ge "{finishAfterPolls}" ]; then echo 0; fi
                exit 0 ;;
              *"tail -n +"*)
                n=$(cat "{polls}" 2>/dev/null || echo 0)
                n=$((n+1)); echo "$n" > "{polls}"
                if [ "$n" -le "{finishAfterPolls}" ]; then echo "working $n"; fi
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

        var host = new AcaAgentProcessHost(
            new AcaSandboxOptions
            {
                CommandPath = script,
                SandboxGroup = group,
                Disk = "claude",
                EgressAllow = ["github.com"],
                PollInterval = TimeSpan.FromMilliseconds(20),
            },
            new RunPreviewHost(),
            NullLogger<AcaAgentProcessHost>.Instance
        );

        return (host, ledger);
    }
}
