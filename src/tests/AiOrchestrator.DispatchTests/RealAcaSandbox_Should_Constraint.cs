using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.ServiceDefaults.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// The shipped <see cref="AcaAgentProcessHost"/> against <b>real Azure</b> — the exercise task 7.2
/// asks for, and the one thing the stand-in script can never provide: the script proves the host
/// calls what it should, not that the platform answers as the design believes.
/// <para>
/// Gated on an environment variable rather than a skip attribute, exactly as
/// <see cref="RealSbxSandbox_Should_Constraint"/> is, so CI can never run it by accident and a
/// human can run it on purpose:
/// <code>
/// AIO_ACA_EXERCISE=1 AIO_ACA_GROUP=aio-exercise dotnet test
///     src/tests/AiOrchestrator.DispatchTests --filter RealAcaSandbox_Should_Constraint
/// </code>
/// </para>
/// <para>
/// <b>Every assertion here is about the boundary, not about an agent.</b> That is deliberate and
/// it is what lets this run at all: the organisation's Anthropic key is not the developer's to
/// mint, so the `claude` disk is unavailable. What this change owns — the exec ceiling absorbed,
/// output arriving while work happens, the platform defaults overridden, the workspace sent
/// rather than mounted, the preview relayed then gone, nothing surviving — needs no model
/// credential to be true or false. A shell command stands in for the agent, and every one of
/// those properties is still able to fail.
/// </para>
/// <para>Observations belong in the change's evidence, verbatim, including what did not work
/// (ADR-0001).</para>
/// </summary>
public class RealAcaSandbox_Should_Constraint
{
    static bool Enabled => Environment.GetEnvironmentVariable("AIO_ACA_EXERCISE") == "1";

    static string Group => Environment.GetEnvironmentVariable("AIO_ACA_GROUP") ?? "aio-exercise";

    /// <summary>
    /// `ubuntu` rather than `claude`: this exercise needs a machine, not an agent, and the disk
    /// carrying an agent is the one whose credential cannot be minted here.
    /// </summary>
    static string Disk => Environment.GetEnvironmentVariable("AIO_ACA_DISK") ?? "ubuntu";

    /// <summary>
    /// A workspace of this Run's own. Never a shared directory: `SendWorkspace` packs the whole
    /// tree, and pointing it at `/tmp` on macOS made `tar` try to archive a socket — a test
    /// defect that is worth a sentence, because it is also the shape of a real failure. A
    /// workspace that cannot be packed is a Run that cannot run, and the host says so rather
    /// than sending half of one.
    /// </summary>
    static string Workspace() => Directory.CreateTempSubdirectory("aio-aca-exercise-").FullName;

    /// <summary>
    /// The group's credential ids, from `AIO_ACA_CREDENTIALS` — ids, never values. Empty where
    /// the exercise does not need one.
    /// </summary>
    static string[] Credentials =>
        (Environment.GetEnvironmentVariable("AIO_ACA_CREDENTIALS") ?? string.Empty).Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

    static AcaAgentProcessHost Host(
        RunPreviewHost previews,
        string[]? egressAllow = null,
        string[]? credentials = null
    ) =>
        new(
            new AcaSandboxOptions
            {
                CommandPath = Environment.GetEnvironmentVariable("ACA_PATH") ?? "aca",
                SandboxGroup = Group,
                Disk = Disk,
                EgressAllow = egressAllow ?? ["github.com"],
                Credentials = credentials ?? [],
                PollInterval = TimeSpan.FromSeconds(2),
            },
            previews,
            NullLogger<AcaAgentProcessHost>.Instance
        );

    [Fact]
    public async Task ARunLongerThanTheExecCeiling_Should_CompleteAndStreamWhileItWorks()
    {
        if (!Enabled)
        {
            return;
        }

        // 90 seconds: comfortably past the 50–60 s ceiling measured on `aca sandbox exec`, and
        // past the t+41 s at which a sandbox was observed suspending itself while a process wrote
        // inside every second. An implementation that ran one exec, or that left auto-suspend
        // alone, fails here rather than in production.
        var streamed = new List<string>();
        var host = Host(new RunPreviewHost());

        var outcome = await host.Run(
            "sh",
            ["-c", "for i in $(seq 1 90); do echo working $i; sleep 1; done; echo done"],
            Workspace(),
            new Dictionary<string, string>(),
            TimeSpan.FromMinutes(5),
            CancellationToken.None,
            streamed.Add
        );

        outcome.TimedOut.ShouldBeFalse();
        outcome.ExitCode.ShouldBe(0);

        // Arriving while it worked, not in one lump at the end (UC-027, #96).
        streamed.ShouldContain(line => line.Contains("working 1"));
        streamed.ShouldContain(line => line.Contains("working 89"));
    }

    [Fact]
    public async Task TheWorkspace_Should_ArriveWithoutTheExecutorSharingItsMachine()
    {
        if (!Enabled)
        {
            return;
        }

        // The property this whole change exists for (task 3.2). The sandbox is created remotely
        // over an authenticated API; no mount, no socket, no grant on this machine. If the
        // workspace could only arrive by co-location, this is where that shows.
        var workspace = Workspace();
        await File.WriteAllTextAsync(
            Path.Combine(workspace, "prepared.txt"),
            "prepared by the executor, not by the sandbox"
        );

        var streamed = new List<string>();
        var host = Host(new RunPreviewHost());

        var outcome = await host.Run(
            "cat",
            ["prepared.txt"],
            workspace,
            new Dictionary<string, string>(),
            TimeSpan.FromMinutes(3),
            CancellationToken.None,
            streamed.Add
        );

        outcome.ExitCode.ShouldBe(0);
        string.Join('\n', streamed).ShouldContain("prepared by the executor");
    }

    [Fact]
    public async Task EgressOutsideTheAllowList_Should_BeDeniedAndSaidSoInTheRunsOutput()
    {
        if (!Enabled)
        {
            return;
        }

        // Two halves, both measured rather than trusted: the platform's documented deny-by-default
        // is NOT the behaviour of a sandbox as created (a sandbox with no policy reached
        // example.com and pypi.org with 200s), so the launcher declares the policy — and a policy
        // nobody can audit is half a security story, so what it denied reaches the Run's output.
        var streamed = new List<string>();
        var host = Host(new RunPreviewHost(), ["github.com"]);

        var outcome = await host.Run(
            "sh",
            [
                "-c",
                "curl -s -o /dev/null -w 'example=%{http_code}\\n' --max-time 20 https://example.com "
                    + "|| echo 'example=refused'",
            ],
            Workspace(),
            new Dictionary<string, string>(),
            TimeSpan.FromMinutes(3),
            CancellationToken.None,
            streamed.Add
        );

        outcome.TimedOut.ShouldBeFalse();

        var output = string.Join('\n', streamed);
        output.ShouldNotContain("example=200");
        output.ShouldContain("[egress]");
        output.ShouldContain("example.com");
    }

    [Fact]
    public async Task ARunThatEnds_Should_LeaveNoSandboxAndNoReachablePreview()
    {
        if (!Enabled)
        {
            return;
        }

        // run-previews' contract, on this substrate: reachable while the Run lives, nothing
        // afterwards — not a stale route, not the option. And an abandoned sandbox is both a leak
        // and a bill.
        var previews = new RunPreviewHost();
        var runId = Guid.NewGuid();
        var host = Host(previews);

        var before = await SandboxCount();

        await host.Run(
            "sh",
            ["-c", "echo serving; sleep 5"],
            Workspace(),
            new Dictionary<string, string>(),
            TimeSpan.FromMinutes(3),
            CancellationToken.None,
            onOutput: null,
            preview: new RunPreview(runId, 8080)
        );

        // The ledger forgot it before disposal was even attempted, so no record can point at a
        // port nothing serves.
        previews.PortFor(runId).ShouldBeNull();

        (await SandboxCount()).ShouldBe(before);
    }

    [Fact]
    public async Task AGroupCredential_Should_ReachTheAgentWithoutItsValueEnteringTheSandbox()
    {
        if (!Enabled)
        {
            return;
        }

        // Task 4.2, and the property that makes this substrate worth adopting: the platform holds
        // the token and injects it at its own egress boundary, so nothing of it is inside. The
        // pod path could not offer that — it handed the value in as an environment variable.
        //
        // **Asserted without the value, on purpose.** This test never learns the token; it looks
        // for the *shape* every GitHub fine-grained PAT has. That keeps the secret out of the
        // repository, out of the CI log and out of this process, and it can still fail: were the
        // platform to inject the credential as an environment variable, `github_pat_` would be
        // sitting in the output.
        // Without a credential attached, this would assert that an absent secret is absent —
        // which is no assertion at all (ADR-0013).
        Credentials.Length.ShouldBeGreaterThan(0);

        var streamed = new List<string>();
        var host = Host(new RunPreviewHost(), credentials: Credentials);

        var outcome = await host.Run(
            "sh",
            [
                "-c",
                "env | sort; echo '--- files ---'; "
                    + "grep -rl 'github_pat_' $HOME /etc /tmp 2>/dev/null | head -20; echo '--- end ---'",
            ],
            Workspace(),
            new Dictionary<string, string>(),
            TimeSpan.FromMinutes(3),
            CancellationToken.None,
            streamed.Add
        );

        outcome.TimedOut.ShouldBeFalse();

        var inside = string.Join('\n', streamed);
        // The probe ran to completion inside the sandbox; without this, an empty output would
        // read as "no credential found".
        inside.Contains("--- end ---", StringComparison.Ordinal).ShouldBeTrue();
        inside.Contains("github_pat_", StringComparison.Ordinal).ShouldBeFalse();
    }

    static async Task<int> SandboxCount()
    {
        var listed = await HeadlessProcess.Run(
            Environment.GetEnvironmentVariable("ACA_PATH") ?? "aca",
            ["sandbox", "list", "--group", Group, "-o", "json"],
            Environment.CurrentDirectory,
            new Dictionary<string, string>(),
            TimeSpan.FromSeconds(60),
            CancellationToken.None
        );

        listed.ExitCode.ShouldBe(0, listed.Stderr);

        // Counted from the top-level `id` of each entry, not from "every GUID in the output".
        // The first version did the latter and counted two per sandbox, because each entry also
        // carries its disk image's id under `sourcesRef` — a before/after comparison survived
        // that, but an assertion that cannot say what it counted is not one worth trusting.
        using var document = JsonDocument.Parse(
            string.IsNullOrWhiteSpace(listed.Stdout) ? "[]" : listed.Stdout
        );

        return document.RootElement.ValueKind == JsonValueKind.Array
            ? document
                .RootElement.EnumerateArray()
                .Count(entry => entry.TryGetProperty("id", out _))
            : 0;
    }
}
