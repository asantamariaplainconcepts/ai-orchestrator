using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.ServiceDefaults.Agents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// The agent sandbox seam: where the CLI runs is a habitat's choice, what may cross the boundary
/// is not. The sbx CLI is stood in for by shell scripts — the repository's existing idiom for
/// exercising a process contract without the real binary — and each stand-in is written so it
/// CAN fail: the refusal tests use a script that reports no secret, and would go green only if
/// the production code stopped checking.
/// </summary>
public class AgentSandbox_Should_Constraint
{
    // ---- The credential contract (design D2) ----

    [Fact]
    public void ALocalHost_Should_PassCredentialValues()
    {
        var environment = AgentCredentialEnvironment.For(
            new LocalAgentProcessHost(),
            new AgentCredentials(VendorAccessToken: "t", AiApiKey: "k"),
            aiKeyVariable: "ANTHROPIC_API_KEY"
        );

        environment["GITHUB_TOKEN"].ShouldBe("t");
        environment["ANTHROPIC_API_KEY"].ShouldBe("k");
    }

    [Fact]
    public void ALocalHostWithNoAiKey_Should_ExportNoVariableAtAll()
    {
        // #279's hazard: an exported empty key shadows the CLI's own session auth, which is
        // exactly what the switched-off credential exists to use.
        var environment = AgentCredentialEnvironment.For(
            new LocalAgentProcessHost(),
            new AgentCredentials(VendorAccessToken: "t", AiApiKey: string.Empty),
            aiKeyVariable: "ANTHROPIC_API_KEY"
        );

        environment.ShouldNotContainKey("ANTHROPIC_API_KEY");
        environment["GITHUB_TOKEN"].ShouldBe("t");
    }

    [Fact]
    public void AnInjectingHost_Should_ReceiveNoCredentialValues()
    {
        // The whole point of the boundary: handing values to a host that authenticates on the
        // agent's behalf would put back exactly what the sandbox exists to keep out.
        var environment = AgentCredentialEnvironment.For(
            SbxHost(SbxScript(secretsListed: "github")),
            new AgentCredentials(VendorAccessToken: "t", AiApiKey: "k"),
            aiKeyVariable: "ANTHROPIC_API_KEY"
        );

        environment.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnInjectingHostHandedValues_Should_RefuseRatherThanCarryThemIn()
    {
        // A future caller that forgets AgentCredentialEnvironment must not silently smuggle a
        // token into the sandbox.
        var host = SbxHost(SbxScript(secretsListed: "github"));

        var refusal = await Should.ThrowAsync<AgentProcessHostException>(() =>
            host.Run(
                "claude",
                ["-p", "x"],
                Path.GetTempPath(),
                new Dictionary<string, string> { ["GITHUB_TOKEN"] = "t" },
                TimeSpan.FromSeconds(5),
                CancellationToken.None
            )
        );

        refusal.Message.ShouldContain("defeat the boundary");
    }

    // ---- The precondition that stops an unauthenticated agent (design D2's failure mode) ----

    [Fact]
    public async Task AnInjectingHostWithNoStoredSecret_Should_RefuseNamingTheRemedy()
    {
        // The dangerous failure: the agent runs unauthenticated and fails deep inside the Run
        // for a reason that reads like a repository problem. It must refuse BEFORE starting.
        var host = SbxHost(SbxScript(secretsListed: string.Empty));

        var refusal = await Should.ThrowAsync<AgentProcessHostException>(() =>
            host.Run(
                "claude",
                ["-p", "x"],
                Path.GetTempPath(),
                new Dictionary<string, string>(),
                TimeSpan.FromSeconds(5),
                CancellationToken.None
            )
        );

        refusal.Message.ShouldContain("github");
        refusal.Message.ShouldContain("secret set");
        refusal.Message.ShouldContain("unauthenticated");
    }

    [Fact]
    public async Task AStoppedDaemon_Should_RefuseNamingHowToStartIt()
    {
        var host = SbxHost(SbxScript(secretsListed: "github", daemonRunning: false));

        var refusal = await Should.ThrowAsync<AgentProcessHostException>(() =>
            host.Run(
                "claude",
                ["-p", "x"],
                Path.GetTempPath(),
                new Dictionary<string, string>(),
                TimeSpan.FromSeconds(5),
                CancellationToken.None
            )
        );

        refusal.Message.ShouldContain("daemon start");
    }

    [Fact]
    public async Task AnAbsentSbxBinary_Should_RefuseNamingTheConfigurationKey()
    {
        var host = SbxHost("/nonexistent/sbx");

        var refusal = await Should.ThrowAsync<AgentProcessHostException>(() =>
            host.Run(
                "claude",
                ["-p", "x"],
                Path.GetTempPath(),
                new Dictionary<string, string>(),
                TimeSpan.FromSeconds(5),
                CancellationToken.None
            )
        );

        refusal.Message.ShouldContain(AgentSandboxComposition.CommandPathKey);
    }

    [Fact]
    public void TheSelection_Should_CarryHowTheAgentWillAuthenticate()
    {
        // The sentence the executor writes into the transcript comes from here, because the Runs
        // module cannot see composition types. Its value must follow the chosen host, not a
        // constant — otherwise the transcript would keep claiming values travelled after a
        // habitat stopped sending them.
        var local = Host.CreateApplicationBuilder();
        local.AddAgentRuntime();

        var sandboxed = Host.CreateApplicationBuilder();
        sandboxed.Configuration[AgentSandboxComposition.LauncherKey] =
            AgentSandboxComposition.SbxLauncher;
        sandboxed.AddAgentRuntime();

        var localSource = local
            .Build()
            .Services.GetRequiredService<IAgentRuntimeSelector>()
            .For("ClaudeCodeHeadless")!
            .CredentialSource;
        var sandboxedSource = sandboxed
            .Build()
            .Services.GetRequiredService<IAgentRuntimeSelector>()
            .For("OpenCode")!
            .CredentialSource;

        localSource.ShouldContain("agent process's environment");
        sandboxedSource.ShouldContain("no value enters the sandbox");
    }

    // ---- Composition (design D1, D5) ----

    [Fact]
    public void NoLauncherNamed_Should_KeepTheAgentAChildOfThisProcess()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddAgentRuntime();

        var host = builder.Build().Services.GetRequiredService<IAgentProcessHost>();

        host.ShouldBeOfType<LocalAgentProcessHost>();
        host.SuppliesCredentials.ShouldBeFalse();
    }

    [Fact]
    public void TheSbxLauncher_Should_BeSelectedByItsPresenceAlone()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[AgentSandboxComposition.LauncherKey] =
            AgentSandboxComposition.SbxLauncher;
        builder.AddAgentRuntime();

        var host = builder.Build().Services.GetRequiredService<IAgentProcessHost>();

        host.SuppliesCredentials.ShouldBeTrue();
        host.CredentialSource.ShouldContain("no value enters the sandbox");
    }

    [Fact]
    public void AnUnknownLauncher_Should_RefuseRatherThanRunUnsandboxed()
    {
        // A typo must not silently execute agents outside the boundary in a habitat that asked
        // for isolation.
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[AgentSandboxComposition.LauncherKey] = "sbxx";

        var refusal = Should.Throw<InvalidOperationException>(() => builder.AddAgentRuntime());

        refusal.Message.ShouldContain("sbxx");
        refusal.Message.ShouldContain(AgentSandboxComposition.SbxLauncher);
    }

    [Fact]
    public void BothIsolationSubstrates_Should_BeRefusedNamingBoth()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["Dispatch:PodImage"] = "ghcr.io/example/worker:latest";
        builder.Configuration[AgentSandboxComposition.LauncherKey] =
            AgentSandboxComposition.SbxLauncher;

        var refusal = Should.Throw<InvalidOperationException>(() => builder.AddAgentRuntime());

        refusal.Message.ShouldContain("Dispatch:PodImage");
        refusal.Message.ShouldContain(AgentSandboxComposition.LauncherKey);
        refusal.Message.ShouldContain("Remove whichever is not intended");
    }

    // ---- The preview lives exactly as long as its sandbox (run-previews design D1/D2) ----

    [Fact]
    public async Task AFailedRun_Should_LeaveNoPreviewBehind()
    {
        // The property the whole feature rests on: there is no path in which the record outlives
        // the sandbox. The stub refuses to create one, so the Run cannot even start — and the
        // ledger must still be empty afterwards, not merely "usually".
        var previews = new RunPreviewHost();
        var host = SbxHost(SbxScript(secretsListed: "github"), previews);
        var runId = Guid.CreateVersion7();

        await Should.ThrowAsync<AgentProcessHostException>(() =>
            host.Run(
                "claude",
                ["-p", "x"],
                Path.GetTempPath(),
                new Dictionary<string, string>(),
                TimeSpan.FromSeconds(5),
                CancellationToken.None,
                onOutput: null,
                preview: new RunPreview(runId, SandboxPort: 8000)
            )
        );

        previews.PortFor(runId).ShouldBeNull();
    }

    [Fact]
    public void AnUnhostedProcess_Should_SayPreviewsAreNotHostedHere()
    {
        // Distinct from "this Run has no preview": a portal that is not the sandbox host must not
        // imply the Run failed to make one.
        IRunPreviewMonitor unhosted = new UnhostedRunPreviewMonitor();

        unhosted.Hosted.ShouldBeFalse();
        unhosted.PortFor(Guid.CreateVersion7()).ShouldBeNull();
        new RunPreviewHost().Hosted.ShouldBeTrue();
    }

    // ---- Readiness answers for the right machine (design D6) ----

    [Fact]
    public async Task AnUnreadySandboxHost_Should_ReportItsOwnRemedy()
    {
        var readiness = await SbxHost(SbxScript(secretsListed: "github", daemonRunning: false))
            .CheckReadiness(CancellationToken.None);

        readiness.Ready.ShouldBeFalse();
        readiness.Where.ShouldContain("sandbox");
        readiness.Remedy.ShouldNotBeNull().ShouldContain("daemon start");
    }

    [Fact]
    public async Task AReadySandboxHost_Should_NameTheMachineRunsUse()
    {
        var readiness = await SbxHost(SbxScript(secretsListed: "github"))
            .CheckReadiness(CancellationToken.None);

        readiness.Ready.ShouldBeTrue();
        readiness.Where.ShouldContain("sandbox");
        readiness.Remedy.ShouldBeNull();
    }

    [Fact]
    public async Task ASandboxHost_Should_NotAnswerFromThisProcessesPath()
    {
        // The trap this exists to catch: `sh` IS on this process's PATH, so a probe that shelled
        // out locally would report it ready. Runs execute in a sandbox, and this stub's sandbox
        // cannot be created — so the honest answer is "not ready", not "ready because the host
        // machine happens to have it".
        var answered = await SbxHost(SbxScript(secretsListed: "github"))
            .CliAnswers("sh", CancellationToken.None);

        answered.ShouldBeFalse();
    }

    [Fact]
    public async Task ALocalHost_Should_AnswerFromThisProcess()
    {
        // `git`, not `sh`: the check is `<cli> --version`, and Linux's dash rejects the flag
        // ("Illegal option --"), so a shell is a bad question to ask on any runner but macOS.
        // The same trap caught the sandbox-side check first; it applies identically here.
        var present = await new LocalAgentProcessHost().CliAnswers("git", CancellationToken.None);
        var absent = await new LocalAgentProcessHost().CliAnswers(
            "/nonexistent/cli",
            CancellationToken.None
        );

        present.ShouldBeTrue();
        absent.ShouldBeFalse();
    }

    // ---- Stand-ins ----

    static SbxAgentProcessHost SbxHost(string commandPath, RunPreviewHost? previews = null) =>
        new(
            new SbxSandboxOptions
            {
                CommandPath = commandPath,
                Memory = "1g",
                InjectedSecrets = ["github"],
            },
            previews ?? new RunPreviewHost(),
            NullLogger<SbxAgentProcessHost>.Instance
        );

    /// <summary>
    /// A stand-in sbx: it answers `daemon status` and `secret ls` the way the real CLI does, and
    /// exits non-zero for anything else — so a test that got past the preconditions by accident
    /// would fail rather than pass quietly.
    /// </summary>
    static string SbxScript(string secretsListed, bool daemonRunning = true)
    {
        var script = Path.Combine(Directory.CreateTempSubdirectory("sbx-stub-").FullName, "sbx.sh");

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
              "secret ls") echo '{secretsListed}'; exit 0 ;;
            esac
            echo "the stub was asked for '$*', which this test did not expect" >&2
            exit 90

            """
        );

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                script,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        }

        return script;
    }
}
