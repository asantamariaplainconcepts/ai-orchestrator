using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Agents;
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
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = CommandPath,
            WorkingDirectory = instruction.WorkspacePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        process.StartInfo.ArgumentList.Add("-p");
        process.StartInfo.ArgumentList.Add(instruction.Prompt);
        process.StartInfo.ArgumentList.Add("--output-format");
        process.StartInfo.ArgumentList.Add("json");

        // The value lives in this process environment for the child's lifetime and nowhere
        // else — never in the image, the template, or a file (BR-010, design D1).
        process.StartInfo.Environment["ANTHROPIC_API_KEY"] = instruction.Credentials.AiApiKey;
        process.StartInfo.Environment["GITHUB_TOKEN"] = instruction.Credentials.VendorAccessToken;

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => stdout.AppendLine(e.Data);
        process.ErrorDataReceived += (_, e) => stderr.AppendLine(e.Data);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(instruction.Timeout);

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited between the timeout and the kill.
            }

            // BR-005: the phase timeout ends the Run; the reason names the limit that fired.
            return new AgentResult(
                Succeeded: false,
                Log: $"The agent exceeded its {instruction.Timeout.TotalMinutes:0} minute timeout.",
                OutputLink: null,
                Usage: null
            );
        }

        return Parse(process.ExitCode, stdout.ToString(), stderr.ToString());
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
    /// <summary>
    /// Registers the runtime for any host that composes modules: the Runs module's executor
    /// depends on the seam, and DI validation rightly demands the dependency exist even in
    /// hosts that never invoke it.
    /// </summary>
    public static TBuilder AddAgentRuntime<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<IAgentRuntime, ClaudeCodeHeadlessRuntime>();
        return builder;
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
