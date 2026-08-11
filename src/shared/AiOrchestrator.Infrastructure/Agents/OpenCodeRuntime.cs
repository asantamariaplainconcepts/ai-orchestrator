using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// opencode — the second <see cref="IAgentRuntime"/> (DEC-012), which is what proves the seam.
/// The contract is OBSERVED, not guessed (OPN-004 closed against CLI v1.18.6): headless
/// <c>run --format json</c> emits a JSONL event stream; <c>text</c> events carry the reply,
/// <c>step_finish</c> events carry <c>part.tokens</c> and <c>part.cost</c>. Free models
/// (<c>opencode/*-free</c>) run with no credential — absence is configuration, not an error.
/// </summary>
public sealed class OpenCodeRuntime(
    OpenCodeOptions options,
    IAgentProcessHost processHost,
    ILogger<OpenCodeRuntime> logger
) : IAgentRuntime
{
    public const string Command = "opencode";

    /// <summary>Test seam only, same as the Claude Code runtime's — production never sets it.</summary>
    public string CommandPath { get; init; } = Command;

    public async Task<AgentResult> Execute(
        AgentInstruction instruction,
        CancellationToken cancellationToken
    )
    {
        // Values only where the host cannot authenticate for us (design D2); a free model has
        // no key to omit either way (DEC-044). The helper also carries #244 AC6's rule: a Local
        // Run resolves no vendor token, and an exported empty GITHUB_TOKEN would shadow the
        // host tooling's own auth.
        var environment = AgentCredentialEnvironment.For(
            processHost,
            instruction.Credentials,
            aiKeyVariable: "OPENCODE_API_KEY"
        );

        AgentProcessOutcome outcome;
        try
        {
            outcome = await processHost.Run(
                CommandPath,
                // The Run's model where it named one, this deployment's otherwise (#291). The
                // flag is always passed because opencode requires it — what changes is whose
                // answer fills it.
                [
                    "run",
                    "-m",
                    instruction.Model ?? options.Model,
                    "--format",
                    "json",
                    instruction.Prompt,
                ],
                instruction.WorkspacePath,
                environment,
                instruction.Timeout,
                cancellationToken,
                instruction.OnOutput,
                // Forwarded, and it was not before (#296): the executor built the instruction with
                // its preview and neither runtime passed it on, so no Run ever published a port.
                // The gated sbx test missed it by calling the host directly — it exercised the
                // component and not the chain, which is exactly how a missing wire stays invisible.
                instruction.Preview,
                instruction.ProjectId,
                instruction.RunId
            );
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The executable is not there to start — the raw ENOENT told nobody anything
            // (#279): the failure carries the remedy, because nothing retries (BR-004).
            return new AgentResult(
                Succeeded: false,
                Log: AgentRuntimeRemedies.MissingCli(Command, AgentRuntimeRemedies.InstallOpenCode),
                OutputLink: null,
                Usage: null
            );
        }
        catch (AgentProcessHostException exception)
        {
            // The boundary refused before any agent ran; its message names the remedy (BR-004).
            return new AgentResult(
                Succeeded: false,
                Log: exception.Message,
                OutputLink: null,
                Usage: null
            );
        }

        if (outcome.TimedOut)
        {
            return new AgentResult(
                Succeeded: false,
                Log: $"The agent exceeded its {instruction.Timeout.TotalMinutes:0} minute timeout.",
                OutputLink: null,
                Usage: null
            );
        }

        return Parse(outcome);
    }

    AgentResult Parse(AgentProcessOutcome outcome)
    {
        var log = new List<string>();
        long inputTokens = 0;
        long outputTokens = 0;
        decimal cost = 0;
        var sawStepFinish = false;
        var parsedAny = false;

        foreach (var line in outcome.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                // The stream may interleave non-event noise; only events count (design D4).
                continue;
            }

            using (document)
            {
                parsedAny = true;
                var root = document.RootElement;
                var type = root.TryGetProperty("type", out var typeElement)
                    ? typeElement.GetString()
                    : null;

                if (
                    type == "text"
                    && root.TryGetProperty("part", out var textPart)
                    && textPart.TryGetProperty("text", out var text)
                )
                {
                    log.Add(text.GetString() ?? string.Empty);
                }

                if (type == "step_finish" && root.TryGetProperty("part", out var finishPart))
                {
                    // Observed shape; every miss simply contributes nothing — BR-011 turns an
                    // empty aggregation into "unknown", never invented numbers.
                    if (
                        finishPart.TryGetProperty("tokens", out var tokens)
                        && tokens.TryGetProperty("input", out var input)
                        && tokens.TryGetProperty("output", out var output)
                    )
                    {
                        sawStepFinish = true;
                        inputTokens += input.GetInt64();
                        outputTokens += output.GetInt64();
                    }

                    if (finishPart.TryGetProperty("cost", out var costElement))
                    {
                        cost += costElement.GetDecimal();
                    }
                }
            }
        }

        if (outcome.ExitCode != 0 || !parsedAny)
        {
            RuntimeLog.OpenCodeFailed(logger, outcome.ExitCode);
            return new AgentResult(
                Succeeded: false,
                Log: $"exit {outcome.ExitCode}; stdout: {outcome.Stdout}; stderr: {outcome.Stderr}",
                OutputLink: null,
                Usage: null
            );
        }

        return new AgentResult(
            Succeeded: true,
            Log: string.Join("\n", log),
            OutputLink: null,
            Usage: sawStepFinish ? new AgentUsage(inputTokens, outputTokens, cost) : null
        );
    }
}

/// <summary>Composed from configuration; the default model is a free one (owner input, #30).</summary>
public sealed class OpenCodeOptions
{
    public required string Model { get; init; }
}

static partial class RuntimeLog
{
    [LoggerMessage(
        EventId = 4102,
        Level = LogLevel.Error,
        Message = "opencode produced no readable events (exit {ExitCode}) — the run is failed with the raw streams as evidence"
    )]
    public static partial void OpenCodeFailed(ILogger logger, int exitCode);
}
