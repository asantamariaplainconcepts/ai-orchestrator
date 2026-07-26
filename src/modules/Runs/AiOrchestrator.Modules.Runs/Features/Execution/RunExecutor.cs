using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Secrets;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Projects.Contracts;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.Modules.Runs.Features.Execution;

/// <summary>
/// Executes one claimed Run to a terminal state (agent-execution spec). Everything the runtime
/// needs is assembled here, in process: the Story and Automation through Contracts, the
/// credentials resolved <b>by name</b> at the last moment (design D1) — the queue message
/// carried only the Run id, and nothing secret survives this scope.
/// </summary>
sealed class RunExecutor(
    RunsDbContext database,
    IStoryReader stories,
    IAutomationCatalog automations,
    IConnectorReader connectors,
    ISecretResolver secrets,
    IAgentRuntime runtime,
    RunsOptions options,
    TimeProvider clock,
    ILogger<RunExecutor> logger
) : IRunExecutor
{
    public async Task Execute(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await database.Runs.FindAsync([runId], cancellationToken);

        if (run is null)
        {
            // The message is already deleted (BR-004): a stale or foreign id is logged and
            // dropped, never retried into existence.
            ExecutionLog.RunNotFound(logger, runId);
            return;
        }

        if (run.State != RunState.Queued)
        {
            ExecutionLog.NotQueued(logger, runId, run.State.ToString());
            return;
        }

        run.MarkExecuting(clock.GetUtcNow());
        await database.SaveChangesAsync(cancellationToken);

        try
        {
            var result = await Invoke(run, cancellationToken);

            if (result.Succeeded)
            {
                run.Succeed(
                    clock.GetUtcNow(),
                    result.Usage?.InputTokens,
                    result.Usage?.OutputTokens,
                    result.Usage?.CostUsd
                );
                ExecutionLog.Succeeded(logger, runId, result.Usage is null);
            }
            else
            {
                run.Fail(clock.GetUtcNow(), Truncate(result.Log));
                ExecutionLog.Failed(logger, runId);
            }
        }
        catch (Exception exception)
        {
            // A crash between Executing and a terminal state must still end the Run: nothing
            // will redeliver (BR-004), so an eternal Executing would hold the Story hostage.
            run.Fail(clock.GetUtcNow(), Truncate(exception.Message));
            ExecutionLog.Crashed(logger, exception, runId);
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    async Task<AgentResult> Invoke(Run run, CancellationToken cancellationToken)
    {
        var story = await stories.Find(run.ProjectId, run.VendorStoryId, cancellationToken);
        if (story is null)
        {
            return new AgentResult(
                Succeeded: false,
                Log: "The mirrored story no longer exists.",
                OutputLink: null,
                Usage: null
            );
        }

        var automation = await automations.Detail(
            run.ProjectId,
            run.AutomationId,
            cancellationToken
        );
        if (automation is null)
        {
            return new AgentResult(
                Succeeded: false,
                Log: "The automation is no longer enabled on this project.",
                OutputLink: null,
                Usage: null
            );
        }

        var connector = await connectors.Find(run.ProjectId, cancellationToken);
        if (connector is null)
        {
            return new AgentResult(
                Succeeded: false,
                Log: "The project has no connector.",
                OutputLink: null,
                Usage: null
            );
        }

        string vendorToken;
        string aiKey;
        try
        {
            vendorToken = await secrets.Resolve(connector.SecretName, cancellationToken);
            aiKey = await secrets.Resolve(options.AiCredentialSecretName, cancellationToken);
        }
        catch (SecretNotFoundException exception)
        {
            // The name that failed is safe to state; a value never appears (BR-010).
            return new AgentResult(
                Succeeded: false,
                Log: $"Credential could not be resolved: {exception.Message}",
                OutputLink: null,
                Usage: null
            );
        }

        var workspace = Directory.CreateTempSubdirectory("run-").FullName;
        try
        {
            // Deterministic minimal instruction (design D4): #19 owns the real implement→PR
            // content; this proves the contract — prompt in, result and usage out.
            var prompt =
                $"You are executing automation action '{automation.Action}' for story "
                + $"#{story.VendorStoryId} of {connector.Owner}/{connector.Repository}. "
                + $"Story state: {story.State}; labels: {string.Join(", ", story.Labels)}.";

            return await runtime.Execute(
                new AgentInstruction(
                    prompt,
                    automation.Action,
                    automation.Timeout,
                    workspace,
                    new AgentCredentials(vendorToken, aiKey)
                ),
                cancellationToken
            );
        }
        finally
        {
            try
            {
                Directory.Delete(workspace, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp directory is not worth failing a finished Run over.
            }
        }
    }

    static string Truncate(string text) => text.Length <= 1000 ? text : text[..1000];
}

static partial class ExecutionLog
{
    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Warning,
        Message = "Claimed run {RunId} does not exist — dropping (the message is already deleted)"
    )]
    public static partial void RunNotFound(ILogger logger, Guid runId);

    [LoggerMessage(
        EventId = 3102,
        Level = LogLevel.Warning,
        Message = "Claimed run {RunId} is {State}, not Queued — dropping"
    )]
    public static partial void NotQueued(ILogger logger, Guid runId, string state);

    [LoggerMessage(
        EventId = 3103,
        Level = LogLevel.Information,
        Message = "Run {RunId} succeeded (usage unknown: {UsageUnknown})"
    )]
    public static partial void Succeeded(ILogger logger, Guid runId, bool usageUnknown);

    [LoggerMessage(EventId = 3104, Level = LogLevel.Warning, Message = "Run {RunId} failed")]
    public static partial void Failed(ILogger logger, Guid runId);

    [LoggerMessage(
        EventId = 3105,
        Level = LogLevel.Error,
        Message = "Run {RunId} crashed during execution and was marked Failed"
    )]
    public static partial void Crashed(ILogger logger, Exception exception, Guid runId);
}
