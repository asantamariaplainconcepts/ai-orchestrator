using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// Claude Code headless — the first <see cref="IAgentRuntime"/> (DEC-012). The CLI is confined
/// to this file the way Octokit is confined to the GitHub connector: nothing it emits leaves
/// except as the seam's own records.
/// <para>
/// The result parser is deliberately defensive (design D2 is a hypothesis until the
/// in-container spike observes the JSON): any miss on usage or cost yields null, which BR-011
/// renders as "unknown" — a wrong hypothesis degrades to honesty, not to a failed Run.
/// </para>
/// </summary>
public sealed class ClaudeCodeHeadlessRuntime(ILogger<ClaudeCodeHeadlessRuntime> logger)
    : IAgentRuntime
{
    /// <summary>One name for the binary; the image pins its version (design D5).</summary>
    public const string Command = "claude";

    /// <summary>
    /// The executable actually started. A test seam only: BR-005's kill-on-timeout can be
    /// exercised honestly with a process that sleeps, which the pinned CLI cannot be asked to
    /// do without a credential. Production composition never sets it.
    /// </summary>
    public string CommandPath { get; init; } = Command;

    public async Task<AgentResult> Execute(
        AgentInstruction instruction,
        CancellationToken cancellationToken
    )
    {
        var outcome = await HeadlessProcess.Run(
            CommandPath,
            ["-p", instruction.Prompt, "--output-format", "json"],
            instruction.WorkspacePath,
            new Dictionary<string, string>
            {
                // The values live in the child's environment for its lifetime and nowhere
                // else — never in the image, the template, or a file (BR-010, design D1).
                ["ANTHROPIC_API_KEY"] = instruction.Credentials.AiApiKey,
                ["GITHUB_TOKEN"] = instruction.Credentials.VendorAccessToken,
            },
            instruction.Timeout,
            cancellationToken
        );

        if (outcome.TimedOut)
        {
            // BR-005: the phase timeout ends the Run; the reason names the limit that fired.
            return new AgentResult(
                Succeeded: false,
                Log: $"The agent exceeded its {instruction.Timeout.TotalMinutes:0} minute timeout.",
                OutputLink: null,
                Usage: null
            );
        }

        return Parse(outcome.ExitCode, outcome.Stdout, outcome.Stderr);
    }

    AgentResult Parse(int exitCode, string stdout, string stderr)
    {
        try
        {
            using var document = JsonDocument.Parse(stdout);
            var root = document.RootElement;

            var isError =
                root.TryGetProperty("is_error", out var errorFlag) && errorFlag.GetBoolean();
            var log = root.TryGetProperty("result", out var result)
                ? (result.GetString() ?? string.Empty)
                : stdout;

            return new AgentResult(
                Succeeded: exitCode == 0 && !isError,
                Log: log,
                OutputLink: null,
                Usage: ParseUsage(root)
            );
        }
        catch (JsonException)
        {
            // Unreadable output is a failed contract, and saying so beats guessing. The raw
            // streams are the only evidence there is.
            RuntimeLog.UnparseableOutput(logger, exitCode);
            return new AgentResult(
                Succeeded: false,
                Log: $"exit {exitCode}; stdout: {stdout}; stderr: {stderr}",
                OutputLink: null,
                Usage: null
            );
        }
    }

    static AgentUsage? ParseUsage(JsonElement root)
    {
        // Hypothesis shape (design D2): total_cost_usd at the root, usage.{input_tokens,
        // output_tokens}. Every miss returns null — BR-011 turns null into "unknown".
        if (
            !root.TryGetProperty("usage", out var usage)
            || !usage.TryGetProperty("input_tokens", out var input)
            || !usage.TryGetProperty("output_tokens", out var output)
            || !root.TryGetProperty("total_cost_usd", out var cost)
        )
        {
            return null;
        }

        return new AgentUsage(input.GetInt64(), output.GetInt64(), cost.GetDecimal());
    }
}

public static class AgentRuntimeComposition
{
    /// <summary>Claude Code's credential name (DEC-014 — the vault holds the value).</summary>
    public const string ClaudeCredentialKey = "Agents:ClaudeCodeHeadless:CredentialSecretName";

    /// <summary>opencode's credential name; EMPTY by default — free models need none (D3).</summary>
    public const string OpenCodeCredentialKey = "Agents:OpenCode:CredentialSecretName";

    public const string OpenCodeModelKey = "Agents:OpenCode:Model";

    /// <summary>
    /// Registers every runtime and the selector that maps an Automation's runtime name to one
    /// of them (opencode-runtime design D1). Any host composing modules registers this: the
    /// Runs module's executor depends on the seam, and DI validation rightly demands it.
    /// </summary>
    public static TBuilder AddAgentRuntime<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<ClaudeCodeHeadlessRuntime>();
        builder.Services.AddSingleton(
            new OpenCodeOptions
            {
                Model = builder.Configuration.GetValue(
                    OpenCodeModelKey,
                    defaultValue: "opencode/deepseek-v4-flash-free"
                )!,
            }
        );
        builder.Services.AddSingleton<OpenCodeRuntime>();

        var claudeCredential = builder.Configuration.GetValue(
            ClaudeCredentialKey,
            defaultValue: "anthropic-api-key"
        );
        var openCodeCredential = builder.Configuration.GetValue<string?>(
            OpenCodeCredentialKey,
            defaultValue: null
        );

        builder.Services.AddSingleton<IAgentRuntimeSelector>(provider => new AgentRuntimeSelector(
            new Dictionary<string, AgentRuntimeSelection>(StringComparer.Ordinal)
            {
                ["ClaudeCodeHeadless"] = new(
                    provider.GetRequiredService<ClaudeCodeHeadlessRuntime>(),
                    claudeCredential
                ),
                ["OpenCode"] = new(
                    provider.GetRequiredService<OpenCodeRuntime>(),
                    string.IsNullOrWhiteSpace(openCodeCredential) ? null : openCodeCredential
                ),
            }
        ));

        return builder;
    }

    sealed class AgentRuntimeSelector(IReadOnlyDictionary<string, AgentRuntimeSelection> runtimes)
        : IAgentRuntimeSelector
    {
        public AgentRuntimeSelection? For(string runtimeName) =>
            runtimes.TryGetValue(runtimeName, out var selection) ? selection : null;
    }
}

static partial class RuntimeLog
{
    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Error,
        Message = "The agent CLI produced unparseable output (exit {ExitCode}) — the run is failed with the raw streams as evidence"
    )]
    public static partial void UnparseableOutput(ILogger logger, int exitCode);
}
