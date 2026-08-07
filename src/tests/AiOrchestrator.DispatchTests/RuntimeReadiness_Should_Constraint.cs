using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.ServiceDefaults.Agents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// #279 — a runtime's credential requirement can be switched off, and its absence-of-CLI failure
/// names the remedy. What is asserted is the truth-telling contract, never the CLIs themselves:
/// the processes spawned here are <c>/usr/bin/env</c> and a path that cannot exist.
/// </summary>
public class RuntimeReadiness_Should_Constraint
{
    [Fact]
    public void EmptyCredentialConfig_Should_MeanNoSecretForBothRuntimes()
    {
        // Before this change Claude's hard default ('anthropic-api-key') had no off switch:
        // empty config still reached Resolve(""). Empty must mean "the machine's own session".
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[AgentRuntimeComposition.ClaudeCredentialKey] = "";
        builder.Configuration[AgentRuntimeComposition.OpenCodeCredentialKey] = "  ";
        builder.AddAgentRuntime();

        var selector = builder.Build().Services.GetRequiredService<IAgentRuntimeSelector>();

        selector.For("ClaudeCodeHeadless")!.CredentialSecretName.ShouldBeNull();
        selector.For("OpenCode")!.CredentialSecretName.ShouldBeNull();
    }

    [Fact]
    public void TheDefault_Should_StillBeTheSecretName()
    {
        // The off switch is the operator's act; an untouched configuration keeps today's
        // behaviour exactly.
        var builder = Host.CreateApplicationBuilder();
        builder.AddAgentRuntime();

        var selector = builder.Build().Services.GetRequiredService<IAgentRuntimeSelector>();

        selector.For("ClaudeCodeHeadless")!.CredentialSecretName.ShouldBe("anthropic-api-key");
        // Every registration is enumerable for the probe (#279): observability reads the same
        // dictionary selection does.
        selector.Registered.Keys.ShouldBe(["ClaudeCodeHeadless", "OpenCode"], ignoreOrder: true);
    }

    [Fact]
    public async Task NoAiKey_Should_ExportNoAnthropicVariableAtAll()
    {
        // An exported empty ANTHROPIC_API_KEY shadows the CLI's own session auth — the exact
        // state the switched-off credential exists to use. The stand-in process prints the
        // child's environment (ignoring the runtime's arguments); its output is unparseable as
        // a result stream, so the runtime fails the run and quotes the raw stdout — which must
        // not name the variable.
        var runtime = new ClaudeCodeHeadlessRuntime(NullLogger<ClaudeCodeHeadlessRuntime>.Instance)
        {
            CommandPath = PrintEnvScript(),
        };

        var result = await runtime.Execute(
            Instruction(aiKey: string.Empty),
            CancellationToken.None
        );

        result.Succeeded.ShouldBeFalse();
        result.Log.ShouldNotContain("ANTHROPIC_API_KEY");
        result.Log.ShouldContain("GITHUB_TOKEN");
    }

    [Fact]
    public async Task AResolvedKey_Should_StillTravel()
    {
        var runtime = new ClaudeCodeHeadlessRuntime(NullLogger<ClaudeCodeHeadlessRuntime>.Instance)
        {
            CommandPath = PrintEnvScript(),
        };

        var result = await runtime.Execute(Instruction(aiKey: "k"), CancellationToken.None);

        result.Log.ShouldContain("ANTHROPIC_API_KEY");
    }

    /// <summary>A stand-in agent CLI that prints its environment whatever its arguments are.</summary>
    static string PrintEnvScript()
    {
        var script = Path.Combine(Path.GetTempPath(), $"print-env-{Guid.NewGuid():N}.sh");
        File.WriteAllText(script, "#!/bin/sh\nenv\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                script,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        }
        return script;
    }

    [Fact]
    public async Task AMissingCli_Should_FailNamingTheRemedy()
    {
        // The raw ENOENT told nobody anything (#279): the failure carries the binary and the
        // pinned install command, for both runtimes, from the one place the sentences live.
        var claude = new ClaudeCodeHeadlessRuntime(NullLogger<ClaudeCodeHeadlessRuntime>.Instance)
        {
            CommandPath = "/nonexistent/claude",
        };
        var opencode = new OpenCodeRuntime(
            new OpenCodeOptions { Model = "m" },
            NullLogger<OpenCodeRuntime>.Instance
        )
        {
            CommandPath = "/nonexistent/opencode",
        };

        var claudeResult = await claude.Execute(Instruction(aiKey: ""), CancellationToken.None);
        var openCodeResult = await opencode.Execute(Instruction(aiKey: ""), CancellationToken.None);

        claudeResult.Succeeded.ShouldBeFalse();
        claudeResult.Log.ShouldContain("'claude'");
        claudeResult.Log.ShouldContain(AgentRuntimeRemedies.InstallClaudeCode);
        openCodeResult.Succeeded.ShouldBeFalse();
        openCodeResult.Log.ShouldContain("'opencode'");
        openCodeResult.Log.ShouldContain(AgentRuntimeRemedies.InstallOpenCode);
    }

    static AgentInstruction Instruction(string aiKey) =>
        new(
            Prompt: "p",
            Action: "Estimate",
            Timeout: TimeSpan.FromSeconds(20),
            WorkspacePath: Path.GetTempPath(),
            Credentials: new AgentCredentials(VendorAccessToken: "t", AiApiKey: aiKey)
        );
}
