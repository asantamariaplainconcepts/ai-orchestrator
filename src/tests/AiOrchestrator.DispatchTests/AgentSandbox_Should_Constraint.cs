using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.ServiceDefaults.Agents;
using AiOrchestrator.ServiceDefaults.Dispatch;
using Microsoft.Extensions.Configuration;
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

    // ---- Session carriage, and the habitat that declines it (#288) ----

    [Fact]
    public void CarriageOff_Should_BeTheDefaultForEveryHabitat()
    {
        // The softening must be acquired deliberately, never by a habitat forgetting to unset
        // something. Naming only the launcher gets injection, exactly as before #288.
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[AgentSandboxComposition.LauncherKey] =
            AgentSandboxComposition.SbxLauncher;
        builder.AddAgentRuntime();

        var host = builder.Build().Services.GetRequiredService<IAgentProcessHost>();

        host.CredentialSource.ShouldContain("no value enters the sandbox");
        host.CredentialSource.ShouldNotContain("owner");
    }

    [Fact]
    public void CarriageOn_Should_SayTheRunActsAsThatSeat()
    {
        // The transcript's third source. A Run whose spend lands on somebody's own seat has to
        // say so, or a surprising bill is undiagnosable from the Run's own record.
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[AgentSandboxComposition.LauncherKey] =
            AgentSandboxComposition.SbxLauncher;
        builder.Configuration[AgentSandboxComposition.CarrySessionKey] = "true";
        builder.AddAgentRuntime();

        var host = builder.Build().Services.GetRequiredService<IAgentProcessHost>();

        host.CredentialSource.ShouldContain("owner's own session");
        host.CredentialSource.ShouldContain("acts as that seat");
    }

    [Fact]
    public void TheCarriedSet_Should_BeCredentialFilesOnly()
    {
        // Observed 2026-08-08: opencode's whole session is one 950-byte auth.json, while its
        // ~/.config tree is over a gigabyte of caches. Carrying the tree would move all of it for
        // nothing, so the default names files.
        SbxSandboxOptions.DefaultSessionFiles.ShouldContain(".local/share/opencode/auth.json");
        SbxSandboxOptions.DefaultSessionFiles.ShouldAllBe(file => !file.EndsWith('/'));
        // Claude Code's macOS session is in the system keychain and has no file to name; the
        // readiness panel explains it instead of this list pretending to carry it.
        SbxSandboxOptions.DefaultSessionFiles.ShouldNotContain(file => file.Contains(".claude"));
    }

    [Fact]
    public void ARuntimeWhoseSessionCannotTravel_Should_SayWhyAndHowToFixIt()
    {
        // The half that survives even if carriage were dropped (#288 D6). Claude Code on macOS
        // keeps its session in the system keychain, so no copy reaches it — and "secret missing"
        // is exactly the wrong sentence to show a developer who IS signed in.
        var host = CarryingHost(["claude"]);

        var gap = host.SessionUnavailableFor("ClaudeCodeHeadless", "claude", "anthropic-api-key");

        gap.ShouldNotBeNull();
        gap.Reason.ShouldContain("keychain");
        gap.Reason.ShouldContain("cannot be given a copy");
        // A reason without a remedy leaves the developer where the old silence did.
        // The name the runtime already expects — a remedy inventing a second name would leave
        // the developer with a stored key nothing reads.
        gap.Remedy.ShouldBe("sbx secret set -g anthropic-api-key");
        // Names only, never values (BR-010).
        gap.Reason.ShouldNotContain("sk-");
    }

    [Fact]
    public void ARuntimeWhoseSessionIsAFile_Should_RaiseNoSuchQuestion()
    {
        // opencode's session is a file the copy reaches, so there is nothing to warn about — and
        // a panel that warned anyway would train its reader to ignore it.
        var host = CarryingHost(["claude"]);

        host.SessionUnavailableFor("OpenCode", "opencode", null).ShouldBeNull();
    }

    [Fact]
    public void CarriageOff_Should_RaiseNoSuchQuestionEither()
    {
        // Nothing was promised, so nothing is missing: a habitat that injects credentials is not
        // failing to carry a session it never offered to carry.
        // Carriage off is exactly "no session files declared" — the same switch the habitat flips.
        var host = SbxHost("sbx", keychainRuntimes: ["claude"]);

        host.SessionUnavailableFor("ClaudeCodeHeadless", "claude", null).ShouldBeNull();
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
        refusal.Message.ShouldContain(AgentSandboxComposition.AcaLauncher);
    }

    // ---- The Azure launcher, and what it refuses to guess (#296, design D3) ----

    [Fact]
    public void TheAzureLauncher_Should_BeSelectedByItsPresenceAlone()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[AgentSandboxComposition.LauncherKey] =
            AgentSandboxComposition.AcaLauncher;
        builder.Configuration[AgentSandboxComposition.SandboxGroupKey] = "aio-project-1";
        builder.Configuration[$"{AgentSandboxComposition.EgressAllowKey}:0"] = "github.com";
        builder.AddAgentRuntime();

        var host = builder.Build().Services.GetRequiredService<IAgentProcessHost>();

        host.SuppliesCredentials.ShouldBeTrue();
        host.CredentialSource.ShouldContain("no value enters the sandbox");
        // No machine owner exists on a remote host, so #288's question does not arise here.
        host.SessionUnavailableFor("ClaudeCodeHeadless", "claude", null).ShouldBeNull();
    }

    [Fact]
    public void TheAzureLauncherWithNoGroup_Should_RefuseNamingWhy()
    {
        // Per Project, because the platform scopes credentials to the group and #244 promises a
        // Run bills as its own Project. A shared group would break that silently.
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[AgentSandboxComposition.LauncherKey] =
            AgentSandboxComposition.AcaLauncher;
        builder.Configuration[$"{AgentSandboxComposition.EgressAllowKey}:0"] = "github.com";

        var refusal = Should.Throw<InvalidOperationException>(() => builder.AddAgentRuntime());

        refusal.Message.ShouldContain(AgentSandboxComposition.SandboxGroupKey);
        refusal.Message.ShouldContain("own Project's identity");
    }

    [Fact]
    public void TheAzureLauncherWithNoEgressList_Should_RefuseRatherThanRunUnrestricted()
    {
        // The refusal that matters most. Deny-by-default is OPT-IN on that platform — measured
        // 2026-08-08, a sandbox with no policy reached example.com and pypi.org with 200s — so a
        // habitat that says nothing would run its agents unrestricted while believing otherwise.
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[AgentSandboxComposition.LauncherKey] =
            AgentSandboxComposition.AcaLauncher;
        builder.Configuration[AgentSandboxComposition.SandboxGroupKey] = "aio-project-1";

        var refusal = Should.Throw<InvalidOperationException>(() => builder.AddAgentRuntime());

        refusal.Message.ShouldContain(AgentSandboxComposition.EgressAllowKey);
        refusal.Message.ShouldContain("unrestricted");
    }

    [Fact]
    public void ARetiredPodImage_Should_BeRefusedNamingWhatReplacedIt()
    {
        // A key that quietly stopped meaning anything is how a deployment ends up running
        // something nobody chose — so an operator upgrading meets the sentence, not silence.
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[DispatchComposition.PodImageKey] = "ghcr.io/example/worker:latest";
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:aiorchestratordb"] = "Host=localhost;Database=x",
            }
        );

        var refusal = Should.Throw<InvalidOperationException>(() =>
            builder.AddRunDispatchConsumer()
        );

        refusal.Message.ShouldContain(DispatchComposition.PodImageKey);
        refusal.Message.ShouldContain("no longer exists");
        refusal.Message.ShouldContain(AgentSandboxComposition.SbxLauncher);
        refusal.Message.ShouldContain(AgentSandboxComposition.AcaLauncher);
    }

    // ---- The chain, not the component (#296) ----

    [Fact]
    public async Task ARuntime_Should_ForwardWhatTheExecutorGaveIt()
    {
        // The assertion that was missing. run-previews' own test called the host directly, so it
        // proved the host publishes a port and never that a Run reaches the host with one — and
        // neither runtime forwarded `instruction.Preview`, so no Run ever did. A component test
        // cannot see a wire that was never connected.
        var host = new RecordingProcessHost();
        var runtime = new OpenCodeRuntime(
            new OpenCodeOptions { Model = "m" },
            host,
            NullLogger<OpenCodeRuntime>.Instance
        );
        var runId = Guid.CreateVersion7();
        var projectId = Guid.CreateVersion7();

        await runtime.Execute(
            new AgentInstruction(
                "prompt",
                "RepositoryPrompt",
                TimeSpan.FromMinutes(1),
                Path.GetTempPath(),
                new AgentCredentials(string.Empty, string.Empty),
                Preview: new RunPreview(runId, 5173),
                ProjectId: projectId
            ),
            CancellationToken.None
        );

        host.Preview.ShouldNotBeNull().RunId.ShouldBe(runId);
        host.Preview.SandboxPort.ShouldBe(5173);
        host.ProjectId.ShouldBe(projectId);
    }

    /// <summary>Records what a runtime handed it, so "did not forward" is assertable.</summary>
    sealed class RecordingProcessHost : IAgentProcessHost
    {
        public RunPreview? Preview { get; private set; }
        public Guid? ProjectId { get; private set; }

        public Task<AgentProcessOutcome> Run(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            IReadOnlyDictionary<string, string> environment,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            Action<string>? onOutput = null,
            RunPreview? preview = null,
            Guid? projectId = null
        )
        {
            Preview = preview;
            ProjectId = projectId;
            return Task.FromResult(
                new AgentProcessOutcome(TimedOut: false, ExitCode: 0, Stdout: "{}", Stderr: "")
            );
        }

        public bool SuppliesCredentials => true;
        public string CredentialSource => "test";

        public Task<AgentHostReadiness> CheckReadiness(CancellationToken cancellationToken) =>
            Task.FromResult(AgentHostReadiness.Local);

        public Task<bool> CliAnswers(string command, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public SessionCarriageGap? SessionUnavailableFor(
            string runtimeName,
            string command,
            string? credentialSecretName
        ) => null;

        public Task<IReadOnlyList<string>?> ListModels(
            string command,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken
        ) => Task.FromResult<IReadOnlyList<string>?>(null);
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

    /// <summary>A host in a habitat that carries the owner's session (#288).</summary>
    static SbxAgentProcessHost CarryingHost(IReadOnlyList<string> keychainRuntimes) =>
        SbxHost(
            "sbx",
            sessionFiles: SbxSandboxOptions.DefaultSessionFiles,
            keychainRuntimes: keychainRuntimes
        );

    static SbxAgentProcessHost SbxHost(
        string commandPath,
        RunPreviewHost? previews = null,
        IReadOnlyList<string>? sessionFiles = null,
        IReadOnlyList<string>? keychainRuntimes = null
    ) =>
        new(
            new SbxSandboxOptions
            {
                CommandPath = commandPath,
                Memory = "1g",
                InjectedSecrets = ["github"],
                SessionFiles = sessionFiles ?? [],
                KeychainRuntimes = keychainRuntimes ?? [],
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
