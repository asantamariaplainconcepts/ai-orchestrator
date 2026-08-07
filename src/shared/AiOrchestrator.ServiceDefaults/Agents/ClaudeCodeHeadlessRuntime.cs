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
public sealed class ClaudeCodeHeadlessRuntime(
    IAgentProcessHost processHost,
    ILogger<ClaudeCodeHeadlessRuntime> logger
) : IAgentRuntime
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
        // The values live in the child's environment for its lifetime and nowhere else — never
        // in the image, the template, or a file (BR-010, design D1). The AI key only when one
        // was resolved (#279): an exported empty ANTHROPIC_API_KEY shadows the CLI's own session
        // auth, which is exactly what the switched-off credential exists to use. A host that
        // authenticates the agent itself receives no values at all (design D2).
        var environment = AgentCredentialEnvironment.For(
            processHost,
            instruction.Credentials,
            aiKeyVariable: "ANTHROPIC_API_KEY"
        );

        AgentProcessOutcome outcome;
        try
        {
            outcome = await processHost.Run(
                CommandPath,
                // stream-json rather than json (#130): `json` prints one document when the process exits,
                // so the live window stayed empty for the whole Run and then filled with one unbroken
                // line. `--verbose` is what makes the CLI emit the intermediate events at all.
                //
                // This flag and Parse below are ONE change. `json` bought a single well-formed document,
                // and the parser was built on it — swapping the flag alone makes that parse throw, and the
                // catch reports a perfectly good Run as a failure.
                ["-p", instruction.Prompt, "--output-format", "stream-json", "--verbose"],
                instruction.WorkspacePath,
                environment,
                instruction.Timeout,
                cancellationToken,
                instruction.OnOutput
            );
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The executable is not there to start — the raw ENOENT told nobody anything
            // (#279): the failure carries the remedy, because nothing retries (BR-004).
            return new AgentResult(
                Succeeded: false,
                Log: AgentRuntimeRemedies.MissingCli(
                    Command,
                    AgentRuntimeRemedies.InstallClaudeCode
                ),
                OutputLink: null,
                Usage: null
            );
        }
        catch (AgentProcessHostException exception)
        {
            // The boundary refused before any agent ran. Its message already names the remedy,
            // and nothing retries (BR-004) — so it becomes the Run's failure verbatim.
            return new AgentResult(
                Succeeded: false,
                Log: exception.Message,
                OutputLink: null,
                Usage: null
            );
        }

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
        var terminal = TerminalResult(stdout);

        if (terminal is null)
        {
            // A stream with no terminal result event means this parser cannot say what happened, and
            // the pre-existing judgement stands: unreadable output is a failed contract, and saying so
            // beats guessing. Trusting the exit code here would be worse than it looks — the Log of a
            // simple action becomes a comment on somebody's Story, so a "success" carrying raw stream
            // text would publish it. Failing loudly after a CLI change costs a Run; the alternative
            // writes noise into a customer's backlog.
            //
            // Note what this is NOT: a miss on the usage *block* inside a result event still degrades
            // to unknown (BR-011). Only a missing result event is fatal.
            RuntimeLog.UnparseableOutput(logger, exitCode);
            return new AgentResult(
                Succeeded: false,
                Log: $"exit {exitCode}; stdout: {stdout}; stderr: {stderr}",
                OutputLink: null,
                Usage: null
            );
        }

        using var document = terminal;
        var root = document.RootElement;

        var isError = root.TryGetProperty("is_error", out var errorFlag) && errorFlag.GetBoolean();
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

    /// <summary>
    /// The stream's own summary: the last line that parses as an object carrying
    /// <c>type: "result"</c>. Last rather than first, because the terminal one is the one whose usage
    /// is the total. Returns null when there is none — every caller treats that as unknown, never as
    /// a failure.
    /// </summary>
    static JsonDocument? TerminalResult(string stdout)
    {
        foreach (var line in stdout.Split('\n', StringSplitOptions.TrimEntries).Reverse())
        {
            if (line.Length == 0)
            {
                continue;
            }

            JsonDocument candidate;
            try
            {
                candidate = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                // A stream carries lines this parser has no opinion about; skipping one is normal.
                continue;
            }

            if (
                candidate.RootElement.ValueKind == JsonValueKind.Object
                && candidate.RootElement.TryGetProperty("type", out var type)
                && type.ValueEquals("result")
            )
            {
                return candidate;
            }

            candidate.Dispose();
        }

        return null;
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
        // WHERE the agent CLI runs (design D1/D5). The local host is the default and the
        // behaviour of every habitat that names nothing; a sandbox launcher named in
        // configuration replaces it, and naming both a pod image and a launcher is refused.
        AgentSandboxComposition.AddAgentProcessHost(builder);

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

        // BOTH credentials normalize whitespace→null (#279): empty means switched off — no
        // secret resolved, the machine's own session. Claude's default stays the secret name,
        // but the default's grip ends where the operator sets the key to empty; before this,
        // the hard default could not be turned off at all, and a session-authenticated machine
        // could not run the runtime it was signed into.
        var claudeCredential = builder.Configuration.GetValue(
            ClaudeCredentialKey,
            defaultValue: "anthropic-api-key"
        );
        var openCodeCredential = builder.Configuration.GetValue<string?>(
            OpenCodeCredentialKey,
            defaultValue: null
        );

        builder.Services.AddSingleton<IAgentRuntimeSelector>(provider =>
        {
            // The chosen host is what decides whether a credential ever reaches the agent, and
            // the transcript must say so (design D2). Carried on the selection because that is
            // the seam the Runs module can see — it cannot reference composition types.
            var credentialSource = provider
                .GetRequiredService<IAgentProcessHost>()
                .CredentialSource;

            return new AgentRuntimeSelector(
                new Dictionary<string, AgentRuntimeSelection>(StringComparer.Ordinal)
                {
                    ["ClaudeCodeHeadless"] = new(
                        provider.GetRequiredService<ClaudeCodeHeadlessRuntime>(),
                        string.IsNullOrWhiteSpace(claudeCredential) ? null : claudeCredential,
                        ClaudeCodeHeadlessRuntime.Command,
                        AgentRuntimeRemedies.InstallClaudeCode
                    )
                    {
                        CredentialSource = credentialSource,
                    },
                    ["OpenCode"] = new(
                        provider.GetRequiredService<OpenCodeRuntime>(),
                        string.IsNullOrWhiteSpace(openCodeCredential) ? null : openCodeCredential,
                        OpenCodeRuntime.Command,
                        AgentRuntimeRemedies.InstallOpenCode
                    )
                    {
                        CredentialSource = credentialSource,
                    },
                }
            );
        });

        return builder;
    }

    sealed class AgentRuntimeSelector(IReadOnlyDictionary<string, AgentRuntimeSelection> runtimes)
        : IAgentRuntimeSelector
    {
        public AgentRuntimeSelection? For(string runtimeName) =>
            runtimes.TryGetValue(runtimeName, out var selection) ? selection : null;

        public IReadOnlyDictionary<string, AgentRuntimeSelection> Registered => runtimes;
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
