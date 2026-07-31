using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.ServiceDefaults.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// BR-005 at the runtime level: the phase timeout kills the process and the result is a
/// failure naming the limit. The pinned CLI cannot be asked to sleep without a credential, so
/// the runtime's command seam runs a script that does — the kill path is the real one.
/// </summary>
public class AgentRuntimeTimeout_Should_Constraint
{
    [Fact]
    public async Task AnOverrunningProcess_Should_BeKilledAndFailNamingTheTimeout()
    {
        if (OperatingSystem.IsWindows())
        {
            // The job image is Linux; a shell-script stand-in has no Windows equivalent worth
            // faking. The guard also satisfies CA1416.
            return;
        }

        var script = Path.Combine(
            Directory.CreateTempSubdirectory("timeout-").FullName,
            "sleepy.sh"
        );
        await File.WriteAllTextAsync(script, "#!/bin/sh\nsleep 60\n");
        File.SetUnixFileMode(
            script,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        );

        var runtime = new ClaudeCodeHeadlessRuntime(NullLogger<ClaudeCodeHeadlessRuntime>.Instance)
        {
            CommandPath = script,
        };

        var result = await runtime.Execute(
            new AgentInstruction(
                "irrelevant",
                "RepositoryPrompt",
                TimeSpan.FromSeconds(2),
                Path.GetTempPath(),
                new AgentCredentials("token", "key")
            ),
            CancellationToken.None
        );

        result.Succeeded.ShouldBeFalse();
        result.Log.ShouldContain("timeout");
        result.Usage.ShouldBeNull();
    }

    [Fact]
    public async Task UnparseableOutput_Should_FailWithTheRawStreamsAsEvidence()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var script = Path.Combine(
            Directory.CreateTempSubdirectory("garbage-").FullName,
            "garbage.sh"
        );
        await File.WriteAllTextAsync(script, "#!/bin/sh\necho not-json\n");
        File.SetUnixFileMode(
            script,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        );

        var runtime = new ClaudeCodeHeadlessRuntime(NullLogger<ClaudeCodeHeadlessRuntime>.Instance)
        {
            CommandPath = script,
        };

        var result = await runtime.Execute(
            new AgentInstruction(
                "irrelevant",
                "RepositoryPrompt",
                TimeSpan.FromSeconds(30),
                Path.GetTempPath(),
                new AgentCredentials("token", "key")
            ),
            CancellationToken.None
        );

        result.Succeeded.ShouldBeFalse();
        result.Log.ShouldContain("not-json");
        result.Usage.ShouldBeNull();
    }
}
