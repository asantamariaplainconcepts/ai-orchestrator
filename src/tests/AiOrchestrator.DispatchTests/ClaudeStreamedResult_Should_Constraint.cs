using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.ServiceDefaults.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// #130 — Claude Code now runs with <c>--output-format stream-json</c> so its output reaches the log
/// while the Run is still executing. The flag and the parser are one change: the previous parser read
/// the whole of stdout as a single document, and its catch turned an unreadable parse into a
/// <b>failed Run</b>. These tests are what fails if the flag moves without the parser.
/// </summary>
public class ClaudeStreamedResult_Should_Constraint
{
    static ClaudeCodeHeadlessRuntime Runtime(string script) =>
        new(NullLogger<ClaudeCodeHeadlessRuntime>.Instance) { CommandPath = script };

    static AgentInstruction Instruction() =>
        new(
            "irrelevant",
            "RepositoryPrompt",
            TimeSpan.FromSeconds(30),
            Path.GetTempPath(),
            new AgentCredentials("token", "key")
        );

    static async Task<string> Script(string body)
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("cc-").FullName, "fake.sh");
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
    public async Task AStreamedRun_Should_SucceedWithItsReplyAndUsage()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // The job image is Linux; the shell stand-in has no Windows equivalent.
        }

        // NDJSON: intermediate events, then the terminal result. Parsing this whole thing as one
        // document throws — which is exactly what used to fail the Run.
        var script = await Script(
            """
            cat << 'EOF'
            {"type":"system","subtype":"init","session_id":"abc"}
            {"type":"assistant","message":{"content":[{"type":"text","text":"Working on it."}]}}
            {"type":"result","subtype":"success","is_error":false,"result":"Done, and here is why.","usage":{"input_tokens":120,"output_tokens":45},"total_cost_usd":0.0031}
            EOF
            """
        );

        var result = await Runtime(script).Execute(Instruction(), CancellationToken.None);

        result.Succeeded.ShouldBeTrue(result.Log);
        result.Log.ShouldBe("Done, and here is why.");
        result.Usage.ShouldNotBeNull();
        result.Usage.InputTokens.ShouldBe(120);
        result.Usage.OutputTokens.ShouldBe(45);
        result.Usage.CostUsd.ShouldBe(0.0031m);
    }

    [Fact]
    public async Task EveryLine_Should_ReachTheWatcherAsItArrives()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var script = await Script(
            """
            cat << 'EOF'
            {"type":"assistant","message":{"content":[{"type":"text","text":"one"}]}}
            {"type":"assistant","message":{"content":[{"type":"text","text":"two"}]}}
            {"type":"result","subtype":"success","is_error":false,"result":"fin"}
            EOF
            """
        );

        var seen = new List<string>();
        var instruction = Instruction() with { OnOutput = line => seen.Add(line) };

        await Runtime(script).Execute(instruction, CancellationToken.None);

        // Three lines observed as lines, not one document at the end: this is the whole point of the
        // flag, and it is why the live window was empty for the default runtime before (#96, UC-027).
        seen.Count.ShouldBe(3);
        seen[0].ShouldContain("one");
    }

    [Fact]
    public async Task AnErrorResult_Should_FailTheRun()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var script = await Script(
            """
            cat << 'EOF'
            {"type":"result","subtype":"error_during_execution","is_error":true,"result":"the tool refused"}
            EOF
            """
        );

        var result = await Runtime(script).Execute(Instruction(), CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.Log.ShouldBe("the tool refused");
    }

    [Fact]
    public async Task NoTerminalEvent_Should_StillFailWithTheStreamAsEvidence()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // Deliberately NOT trusting the exit code (design D1). A simple action's Log becomes a comment
        // on somebody's Story, so a "success" whose reply is raw stream text would publish noise into
        // a customer's backlog. Failing costs one Run and says why.
        var script = await Script(
            """
            cat << 'EOF'
            {"type":"assistant","message":{"content":[{"type":"text","text":"only chatter"}]}}
            EOF
            """
        );

        var result = await Runtime(script).Execute(Instruction(), CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.Usage.ShouldBeNull();
        result.Log.ShouldContain("only chatter");
    }

    [Fact]
    public async Task AResultEventWithNoUsage_Should_SucceedWithUsageUnknown()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // The distinction that matters: a missing usage *block* degrades to unknown (BR-011), while a
        // missing result *event* is fatal. Only one of those is a broken contract.
        var script = await Script(
            """
            cat << 'EOF'
            {"type":"result","subtype":"success","is_error":false,"result":"no numbers here"}
            EOF
            """
        );

        var result = await Runtime(script).Execute(Instruction(), CancellationToken.None);

        result.Succeeded.ShouldBeTrue(result.Log);
        result.Log.ShouldBe("no numbers here");
        result.Usage.ShouldBeNull();
    }

    [Fact]
    public async Task TheLastResultEvent_Should_BeTheOneRead()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // Last, not first: the terminal event is the one whose usage is the total.
        var script = await Script(
            """
            cat << 'EOF'
            {"type":"result","subtype":"success","is_error":false,"result":"early","usage":{"input_tokens":1,"output_tokens":1},"total_cost_usd":0.0001}
            {"type":"result","subtype":"success","is_error":false,"result":"final","usage":{"input_tokens":9,"output_tokens":9},"total_cost_usd":0.009}
            EOF
            """
        );

        var result = await Runtime(script).Execute(Instruction(), CancellationToken.None);

        result.Log.ShouldBe("final");
        result.Usage!.InputTokens.ShouldBe(9);
    }
}
