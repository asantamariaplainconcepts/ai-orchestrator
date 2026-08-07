using AiOrchestrator.ServiceDefaults.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// The shipped <see cref="SbxAgentProcessHost"/> against the REAL sbx CLI — the manual exercise
/// the design calls for, because CI has no KVM or VMM to run a microVM on (design: "CI cannot
/// exercise this"). Gated on an environment variable rather than skipped by attribute so it is
/// impossible to run by accident and trivial to run on purpose:
/// <code>
/// AIO_SBX_EXERCISE=1 SBX_PATH=~/.local/bin/sbx dotnet test tests/AiOrchestrator.DispatchTests \
///     --filter RealSbxSandbox_Should_Constraint
/// </code>
/// Every assertion here is about the boundary, not about an agent: what crosses it, what the
/// host sees, and whether anything is left behind. Its observed output belongs in the change's
/// evidence (ADR-0001).
/// </summary>
public class RealSbxSandbox_Should_Constraint
{
    static bool Enabled => Environment.GetEnvironmentVariable("AIO_SBX_EXERCISE") == "1";

    static SbxAgentProcessHost Host() =>
        new(
            new SbxSandboxOptions
            {
                CommandPath =
                    Environment.GetEnvironmentVariable("SBX_PATH")
                    ?? Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".local/bin/sbx"
                    ),
                Memory = "4g",
                InjectedSecrets = ["github"],
            },
            new RunPreviewHost(),
            NullLogger<SbxAgentProcessHost>.Instance
        );

    [Fact]
    public async Task TheRealHost_Should_BeReadyAndNameItsMachine()
    {
        if (!Enabled)
        {
            return;
        }

        var readiness = await Host().CheckReadiness(CancellationToken.None);

        readiness.Remedy.ShouldBeNull();
        readiness.Ready.ShouldBeTrue();
        readiness.Where.ShouldContain("sandbox");
    }

    [Fact]
    public async Task ARealSandbox_Should_RunTheCommandAndStreamItsOutput()
    {
        if (!Enabled)
        {
            return;
        }

        // The workspace is the Run's; the sandbox must see it at the same path (design D4).
        var workspace = Directory.CreateTempSubdirectory("sbx-exercise-").FullName;
        await File.WriteAllTextAsync(Path.Combine(workspace, "marker.txt"), "workspace reached\n");

        var streamed = new List<string>();

        var outcome = await Host()
            .Run(
                "sh",
                ["-c", "cat marker.txt; echo pwd=$(pwd); echo token=[${GITHUB_TOKEN}]"],
                workspace,
                new Dictionary<string, string>(),
                TimeSpan.FromMinutes(3),
                CancellationToken.None,
                streamed.Add
            );

        outcome.TimedOut.ShouldBeFalse();
        outcome.ExitCode.ShouldBe(0);

        var said = string.Join("\n", streamed);
        said.ShouldContain("workspace reached"); // the workspace crossed the boundary
        said.ShouldContain($"pwd={workspace}"); // at the same absolute path
        said.ShouldContain("token=[]"); // and the credential did NOT
    }

    [Fact]
    public async Task ARealInnerFailure_Should_TravelWithItsStderr()
    {
        if (!Enabled)
        {
            return;
        }

        var outcome = await Host()
            .Run(
                "sh",
                ["-c", "echo inner-detail >&2; exit 7"],
                Directory.CreateTempSubdirectory("sbx-exercise-").FullName,
                new Dictionary<string, string>(),
                TimeSpan.FromMinutes(3),
                CancellationToken.None
            );

        outcome.ExitCode.ShouldBe(7);
        outcome.Stderr.ShouldContain("inner-detail");
    }

    [Fact]
    public async Task TheRealCliCheck_Should_AnswerFromInsideTheSandbox()
    {
        if (!Enabled)
        {
            return;
        }

        // `git`, not `sh`: the check is `<cli> --version`, and dash's sh rejects the flag
        // outright ("Illegal option --"). The real runtimes both answer it — the spike observed
        // `claude --version` and `opencode --version` — so the probe's assumption holds for
        // every CLI it is actually pointed at; it is only a bad question to ask of a shell.
        var host = Host();

        (await host.CliAnswers("git", CancellationToken.None)).ShouldBeTrue();
        (await host.CliAnswers("definitely-not-a-cli", CancellationToken.None)).ShouldBeFalse();
    }
}
