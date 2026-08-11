using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.ServiceDefaults.Agents;
using AiOrchestrator.ServiceDefaults.Agents.Sbx;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// Sandboxes that outlive the process that made them (2026-08-09).
/// <para>
/// Found on the developer's machine rather than by a test: <c>sbx ls</c> showed <b>31 running
/// sandboxes and 125 GB of disk gone</b>, 25 of them <c>aio-probe-*</c> — one per readiness
/// sweep, created every thirty seconds. Every creation is already paired with a disposal in a
/// <c>finally</c>, and that pairing is correct. What it cannot survive is the process not being
/// there to run it: stop the dev loop mid-sweep and the microVM outlives the only reference
/// anyone had to it. A week of restarts is a full disk.
/// </para>
/// <para>
/// So the host claims its namespace instead: a fresh process removes whatever still carries the
/// names it owns, before creating its first sandbox.
/// </para>
/// </summary>
public class SandboxLeak_Should_Constraint
{
    [Fact]
    public async Task AFreshProcess_Should_RemoveWhatAPreviousOneAbandoned()
    {
        var (host, ledger) = Host();

        await host.CheckReadiness(CancellationToken.None);

        var calls = File.ReadAllLines(ledger);

        // Both of the host's own names, and nothing that is not its to remove.
        calls.ShouldContain(line => line.Contains(" rm ") && line.Contains("aio-probe-abandoned"));
        calls.ShouldContain(line => line.Contains(" rm ") && line.Contains("aio-run-abandoned"));
        calls.ShouldNotContain(line =>
            line.Contains(" rm ") && line.Contains("someone-elses-work")
        );
    }

    [Fact]
    public async Task TheSweep_Should_HappenOncePerProcessRatherThanBeforeEveryRun()
    {
        // A reap before every Run would remove the sandbox of a Run running beside this one.
        var (host, ledger) = Host();

        await host.CheckReadiness(CancellationToken.None);
        await host.CheckReadiness(CancellationToken.None);

        File.ReadAllLines(ledger).Count(line => line.Contains(" ls ")).ShouldBe(1);
    }

    static (SbxAgentProcessHost Host, string Ledger) Host()
    {
        var directory = Directory.CreateTempSubdirectory("sbx-reap-").FullName;
        var ledger = Path.Combine(directory, "calls.log");
        var script = Path.Combine(directory, "sbx.sh");

        File.WriteAllText(
            script,
            $"""
            #!/bin/sh
            echo " $* " >> "{ledger}"
            case "$1 $2" in
              "daemon status") echo 'Status: running'; exit 0 ;;
              "secret ls") echo 'github'; exit 0 ;;
              "rm "*) exit 0 ;;
            esac
            case "$1" in
              ls)
                echo 'SANDBOX                 AGENT      STATUS'
                echo 'aio-probe-abandoned     claude     running'
                echo 'aio-run-abandoned       opencode   running'
                echo 'someone-elses-work      claude     running'
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

        return (
            new SbxAgentProcessHost(
                new SbxSandboxOptions
                {
                    CommandPath = script,
                    Memory = "1g",
                    InjectedSecrets = [],
                    SessionFiles = [],
                },
                new RunPreviewHost(),
                new RunSandboxHost(),
                NullLogger<SbxAgentProcessHost>.Instance
            ),
            ledger
        );
    }
}
