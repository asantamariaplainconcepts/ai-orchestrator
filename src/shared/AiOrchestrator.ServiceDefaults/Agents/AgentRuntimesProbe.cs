using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Secrets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// Asks each registered agent runtime how ready it is, on the cadence the panel promises
/// (#279, the pods probe's sibling). Before this, a runtime's absence was discovered only by a
/// Run failing — the panel turns that into a state with a copyable remedy beside it.
/// <para>
/// Two questions per runtime, because their remedies differ: does the CLI answer
/// (<c>--version</c>, exit code only — parsing output would let a CLI's wording turn a healthy
/// host red), and does the configured credential resolve — asked of the same store the executor
/// uses, never of the configuration (ADR-0004: a green config proves nothing). A runtime with
/// no credential configured is the switched-off state: nothing to resolve, nothing to report
/// but the CLI.
/// </para>
/// </summary>
public sealed class AgentRuntimesProbe(
    AgentRuntimesHost host,
    IAgentRuntimeSelector selector,
    IAgentProcessHost processHost,
    ISecretResolver secrets,
    ILogger<AgentRuntimesProbe> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Transitions are logged, states are not: a healthy machine probed every 30 seconds
        // would otherwise write a diary of nothing changing.
        IReadOnlyList<AgentRuntimeState>? previous = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            // The machine first (design D6): where agents run in sandboxes, a runtime's CLI
            // readiness is a statement about that machine, and asking it while the machine is
            // unreachable would answer for the wrong one.
            var hostState = await CheckHost(stoppingToken);

            var current = new List<AgentRuntimeState>(selector.Registered.Count);
            foreach (var (name, selection) in selector.Registered.OrderBy(pair => pair.Key))
            {
                current.Add(await Probe(name, selection, hostState.Ready, stoppingToken));
            }

            host.RecordProbe(current, hostState);

            if (previous is null || !current.SequenceEqual(previous))
            {
                foreach (var state in current)
                {
                    if (!state.CliReady)
                    {
                        ProbeLog.RuntimeCliMissing(logger, state.Name, state.Command);
                    }
                    else if (state.CredentialReady is false)
                    {
                        ProbeLog.RuntimeCredentialMissing(
                            logger,
                            state.Name,
                            state.CredentialSecretName!
                        );
                    }
                    else
                    {
                        ProbeLog.RuntimeReady(logger, state.Name);
                    }
                }
            }

            previous = current;

            try
            {
                await Task.Delay(AgentRuntimesHost.ProbeInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    async Task<AgentHostState> CheckHost(CancellationToken cancellationToken)
    {
        try
        {
            var readiness = await processHost.CheckReadiness(cancellationToken);
            return new AgentHostState(readiness.Where, readiness.Ready, readiness.Remedy);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A host that cannot even answer is not a ready host — saying so beats reporting
            // the runtimes as though this process were the one that runs them.
            return new AgentHostState(
                Where: "the agent host",
                Ready: false,
                Remedy: $"The agent host could not be asked whether it is ready: {exception.Message}"
            );
        }
    }

    async Task<AgentRuntimeState> Probe(
        string name,
        AgentRuntimeSelection selection,
        bool hostReady,
        CancellationToken cancellationToken
    )
    {
        // An unready host cannot answer for its CLIs, and guessing from this process's PATH
        // would state a truth no Run depends on (design D6). Not-ready here is the honest
        // reading: the host's own remedy is the one that has to be applied first.
        var cliReady = hostReady && await CliAnswers(selection.Command, cancellationToken);

        bool? credentialReady = null;
        if (selection.CredentialSecretName is { } secretName)
        {
            try
            {
                await secrets.Resolve(secretName, cancellationToken);
                credentialReady = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Not found, or the store itself refused — the panel's remedy is the same:
                // the named secret is not resolvable here.
                credentialReady = false;
            }
        }

        // Only when it actually explains something: the host answers null unless carriage is on
        // AND this runtime's session is one a copy cannot reach (#288).
        var carriageGap = processHost.SessionUnavailableFor(name, selection.Command);

        return new AgentRuntimeState(
            Name: name,
            Command: selection.Command,
            CliReady: cliReady,
            InstallCommand: selection.InstallCommand,
            CredentialSecretName: selection.CredentialSecretName,
            CredentialReady: credentialReady
        )
        {
            SessionUnavailableReason = carriageGap?.Reason,
            SessionUnavailableRemedy = carriageGap?.Remedy,
        };
    }

    /// <summary>
    /// Asked of the host, never of this process (design D6): the CLI that matters is the one on
    /// the machine where Runs execute.
    /// </summary>
    async Task<bool> CliAnswers(string command, CancellationToken cancellationToken)
    {
        try
        {
            return await processHost.CliAnswers(command, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Missing, not executable, or refusing to start — one verdict, because the
            // operator's first move is identical: install the CLI where Runs execute.
            return false;
        }
    }
}

static partial class ProbeLog
{
    [LoggerMessage(
        EventId = 6240,
        Level = LogLevel.Warning,
        Message = "Agent runtime {Runtime} unavailable: the CLI '{Command}' is not on this process's PATH"
    )]
    public static partial void RuntimeCliMissing(ILogger logger, string runtime, string command);

    [LoggerMessage(
        EventId = 6241,
        Level = LogLevel.Warning,
        Message = "Agent runtime {Runtime} unavailable: the secret '{Secret}' does not resolve"
    )]
    public static partial void RuntimeCredentialMissing(
        ILogger logger,
        string runtime,
        string secret
    );

    [LoggerMessage(
        EventId = 6242,
        Level = LogLevel.Information,
        Message = "Agent runtime {Runtime} ready"
    )]
    public static partial void RuntimeReady(ILogger logger, string runtime);
}
