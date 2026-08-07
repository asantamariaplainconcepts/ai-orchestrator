using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.ServiceDefaults.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// The opencode parser against the OBSERVED event stream (OPN-004's closure, CLI v1.18.6) —
/// the fixture lines below are the spike's real output shape. Shape drift degrades to
/// usage-unknown, never to invented numbers (design D4).
/// </summary>
public class OpenCodeRuntime_Should_Constraint
{
    static OpenCodeRuntime Runtime(string script) =>
        new(
            new OpenCodeOptions { Model = "opencode/deepseek-v4-flash-free" },
            NullLogger<OpenCodeRuntime>.Instance
        )
        {
            CommandPath = script,
        };

    static AgentInstruction Instruction(AgentCredentials? credentials = null) =>
        new(
            "irrelevant",
            "RepositoryPrompt",
            TimeSpan.FromSeconds(30),
            Path.GetTempPath(),
            credentials ?? new AgentCredentials("token", string.Empty)
        );

    static async Task<string> Script(string body)
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("oc-").FullName, "fake.sh");
        await File.WriteAllTextAsync(path, "#!/bin/sh\n" + body);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        }

        return path;
    }

    [Fact]
    public async Task TheObservedStream_Should_YieldTextAndSummedUsage()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // The job image is Linux; the shell stand-in has no Windows equivalent.
        }

        // Two steps, to prove summation — each line is the spike's observed shape.
        var script = await Script(
            """
            cat << 'EOF'
            {"type":"step_start","timestamp":1,"sessionID":"s","part":{"type":"step-start"}}
            {"type":"text","timestamp":2,"sessionID":"s","part":{"type":"text","text":"ok"}}
            {"type":"step_finish","timestamp":3,"sessionID":"s","part":{"type":"step-finish","reason":"stop","tokens":{"total":30,"input":10,"output":20,"reasoning":0,"cache":{}},"cost":0.5}}
            {"type":"text","timestamp":4,"sessionID":"s","part":{"type":"text","text":"done"}}
            {"type":"step_finish","timestamp":5,"sessionID":"s","part":{"type":"step-finish","reason":"stop","tokens":{"total":3,"input":1,"output":2,"reasoning":0,"cache":{}},"cost":0.25}}
            EOF
            """
        );

        var result = await Runtime(script).Execute(Instruction(), CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        result.Log.ShouldBe("ok\ndone");
        result.Usage.ShouldNotBeNull();
        result.Usage.InputTokens.ShouldBe(11);
        result.Usage.OutputTokens.ShouldBe(22);
        result.Usage.CostUsd.ShouldBe(0.75m);
    }

    [Fact]
    public async Task AStreamWithoutStepFinish_Should_SucceedWithUnknownUsage()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var script = await Script(
            """
            echo '{"type":"text","timestamp":1,"sessionID":"s","part":{"type":"text","text":"ok"}}'
            """
        );

        var result = await Runtime(script).Execute(Instruction(), CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        result.Usage.ShouldBeNull();
    }

    [Fact]
    public async Task AnEmptyOrUnreadableStream_Should_FailWithTheRawStreamsAsEvidence()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var script = await Script("echo garbage-not-json\n");

        var result = await Runtime(script).Execute(Instruction(), CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.Log.ShouldContain("garbage-not-json");
        result.Usage.ShouldBeNull();
    }

    [Fact]
    public async Task EmptyCredentials_Should_NeverBeExportedIntoTheProcessEnvironment()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // #244 AC6 / design D5 — the shadowing defect's pin: a Local Run resolves no vendor
        // token, and a variable EXPORTED EMPTY shadows whatever auth the host's own tooling
        // holds. Set-to-empty is what the defect looked like, so set-to-empty is what the script
        // reports — a value the host shell happens to export is inheritance, not shadowing.
        var script = await Script(
            """
            g=ok; [ -n "${GITHUB_TOKEN+x}" ] && [ -z "$GITHUB_TOKEN" ] && g=empty
            k=ok; [ -n "${OPENCODE_API_KEY+x}" ] && [ -z "$OPENCODE_API_KEY" ] && k=empty
            echo "{\"type\":\"text\",\"timestamp\":1,\"sessionID\":\"s\",\"part\":{\"type\":\"text\",\"text\":\"github:$g key:$k gv:${GITHUB_TOKEN-none} kv:${OPENCODE_API_KEY-none}\"}}"
            """
        );

        var bare = await Runtime(script)
            .Execute(
                Instruction(new AgentCredentials(string.Empty, string.Empty)),
                CancellationToken.None
            );
        bare.Log.ShouldStartWith("github:ok key:ok");

        // And the mirror: a real value still travels — absence is about emptiness, not the vars.
        var credentialed = await Runtime(script)
            .Execute(
                Instruction(new AgentCredentials("vendor-token", "ai-key")),
                CancellationToken.None
            );
        credentialed.Log.ShouldBe("github:ok key:ok gv:vendor-token kv:ai-key");
    }

    [Fact]
    public async Task ANonZeroExit_Should_FailEvenWithReadableEvents()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var script = await Script(
            """
            echo '{"type":"text","timestamp":1,"sessionID":"s","part":{"type":"text","text":"partial"}}'
            exit 3
            """
        );

        var result = await Runtime(script).Execute(Instruction(), CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.Log.ShouldContain("exit 3");
    }
}
