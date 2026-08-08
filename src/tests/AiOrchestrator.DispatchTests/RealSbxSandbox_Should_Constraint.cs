using AiOrchestrator.BuildingBlocks.Agents;
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

    static string SbxPath =>
        Environment.GetEnvironmentVariable("SBX_PATH")
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local/bin/sbx"
        );

    static SbxAgentProcessHost Host(IReadOnlyList<string>? sessionFiles = null) =>
        new(
            new SbxSandboxOptions
            {
                CommandPath = SbxPath,
                Memory = "4g",
                InjectedSecrets = ["github"],
                SessionFiles = sessionFiles ?? [],
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

    [Fact]
    public async Task ARealPreviewPort_Should_BePublishedAndReachableThenGone()
    {
        if (!Enabled)
        {
            return;
        }

        // The whole feature, against the real CLI: publish a port, serve something inside the
        // sandbox, reach it from this machine, and find the record gone when the agent finishes.
        var previews = new RunPreviewHost();
        var host = new SbxAgentProcessHost(
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
            previews,
            NullLogger<SbxAgentProcessHost>.Instance
        );

        var runId = Guid.CreateVersion7();
        var workspace = Directory.CreateTempSubdirectory("sbx-preview-").FullName;
        await File.WriteAllTextAsync(
            Path.Combine(workspace, "index.html"),
            "<h1>served from inside the sandbox</h1>"
        );

        string? reached = null;
        int? publishedWhileRunning = null;

        // Read the port WHILE the agent runs, not after: the record is removed in the same
        // finally that disposes the sandbox, so awaiting first would always find it gone —
        // which is the disposal working, not the publish failing.
        var running = host.Run(
            "sh",
            // Serves, then exits on its own — the agent finishing is what disposes the sandbox
            // and the preview with it.
            ["-c", "python3 -m http.server 8000 --bind 0.0.0.0 >/dev/null 2>&1 & sleep 20"],
            workspace,
            new Dictionary<string, string>(),
            TimeSpan.FromMinutes(3),
            CancellationToken.None,
            onOutput: null,
            preview: new RunPreview(runId, SandboxPort: 8000)
        );

        using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
        {
            for (var attempt = 0; attempt < 40 && reached is null; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                publishedWhileRunning ??= previews.PortFor(runId);

                if (publishedWhileRunning is not { } port)
                {
                    continue;
                }

                try
                {
                    reached = await client.GetStringAsync(
                        $"http://127.0.0.1:{port}/index.html",
                        CancellationToken.None
                    );
                }
                catch (HttpRequestException)
                {
                    // Nothing listening yet — the ordinary early state of a Run whose server has
                    // not started. Keep waiting; the assertions below judge the outcome.
                }
                catch (TaskCanceledException)
                {
                    // The client's own timeout, same meaning.
                }
            }
        }

        var outcome = await running;

        outcome.TimedOut.ShouldBeFalse();
        publishedWhileRunning.ShouldNotBeNull(
            "the sandbox should have published an ephemeral host port while the agent ran"
        );
        reached.ShouldNotBeNull().ShouldContain("served from inside the sandbox");

        // And gone with the sandbox — the property the whole design rests on.
        previews.PortFor(runId).ShouldBeNull();
    }

    // ---- The carried session, against the real CLI (#288) ----

    [Fact]
    public async Task ACarriedSession_Should_AuthenticateTheAgentAsTheMachineOwner()
    {
        if (!Enabled)
        {
            return;
        }

        // The claim the whole change rests on: with carriage declared, the agent inside the
        // sandbox is signed in as the person at this keyboard — no API key stored anywhere, and
        // no value passed in the environment. Asserted through the SHIPPED host, so what is
        // exercised is what a Run would use.
        var before = await Sandboxes();
        var streamed = new List<string>();

        var outcome = await Host(SbxSandboxOptions.DefaultSessionFiles)
            .Run(
                "opencode",
                ["auth", "list"],
                Directory.CreateTempSubdirectory("sbx-carriage-").FullName,
                new Dictionary<string, string>(),
                TimeSpan.FromMinutes(5),
                CancellationToken.None,
                streamed.Add
            );

        outcome.TimedOut.ShouldBeFalse();
        outcome.ExitCode.ShouldBe(0);

        // A CLI that found no session still exits cleanly and lists nothing, so the exit code
        // alone would pass while proving the opposite of the claim.
        var said = string.Join("\n", streamed);
        // The CLI prints the provider's display name, not its slug — asserting the slug passed
        // the negative case for free and would have made that test prove nothing (ADR-0013).
        said.ShouldContain("GitHub Copilot");

        // Nothing outlives the Run — the copy died with the sandbox (design D1).
        (await Sandboxes()).ShouldBe(before);
    }

    [Fact]
    public async Task WithoutCarriage_Should_LeaveTheSandboxSignedOut()
    {
        if (!Enabled)
        {
            return;
        }

        // The assertion above can fail, and this is what proves it: the identical command in a
        // habitat that declared nothing finds no session at all.
        var streamed = new List<string>();

        await Host()
            .Run(
                "opencode",
                ["auth", "list"],
                Directory.CreateTempSubdirectory("sbx-no-carriage-").FullName,
                new Dictionary<string, string>(),
                TimeSpan.FromMinutes(5),
                CancellationToken.None,
                streamed.Add
            );

        string.Join("\n", streamed).ShouldNotContain("GitHub Copilot");
    }

    /// <summary>
    /// What the host has running right now, asked of sbx itself — the only witness to a sandbox
    /// that outlived its Run.
    /// </summary>
    static async Task<string> Sandboxes()
    {
        using var process = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(SbxPath, "ls")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        )!;

        var listed = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return listed;
    }
}
