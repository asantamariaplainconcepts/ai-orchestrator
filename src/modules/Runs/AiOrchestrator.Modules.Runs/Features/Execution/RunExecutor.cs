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
    IAgentRuntimeSelector runtimes,
    ICodeWorkspace workspace,
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

        // The phase router (approval-gate D1): the Run's own record decides. An approval-gated
        // Run that nobody has approved gets phase 1; everything else gets execution.
        var planning = await IsPlanPhase(run, cancellationToken);

        if (planning)
        {
            run.MarkPlanning(clock.GetUtcNow());
        }
        else
        {
            run.MarkExecuting(clock.GetUtcNow());
        }

        await database.SaveChangesAsync(cancellationToken);

        try
        {
            var result = await Invoke(run, planning, cancellationToken);

            if (planning && result.Succeeded)
            {
                // BR-006: the wait is untimed and holds no cap slot — it is not work.
                run.AwaitApproval(clock.GetUtcNow(), Truncate(result.Log, PlanLimit));
                ExecutionLog.AwaitingApproval(logger, runId);
            }
            else if (result.Succeeded)
            {
                run.Succeed(
                    clock.GetUtcNow(),
                    result.OutputLink,
                    result.Usage?.InputTokens,
                    result.Usage?.OutputTokens,
                    result.Usage?.CostUsd
                );
                ExecutionLog.Succeeded(logger, runId, result.Usage is null);
            }
            else
            {
                run.Fail(clock.GetUtcNow(), Truncate(result.Log, FailureLimit));
                ExecutionLog.Failed(logger, runId);
            }
        }
        catch (Exception exception)
        {
            // A crash between Executing and a terminal state must still end the Run: nothing
            // will redeliver (BR-004), so an eternal Executing would hold the Story hostage.
            run.Fail(clock.GetUtcNow(), Truncate(exception.Message, FailureLimit));
            ExecutionLog.Crashed(logger, exception, runId);
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// True when this Run still owes a Plan. Reads the Automation rather than a state flag so
    /// a mid-flight change to the Automation cannot strand a Run in the wrong lane.
    /// </summary>
    async Task<bool> IsPlanPhase(Run run, CancellationToken cancellationToken)
    {
        if (run.ApprovedAt is not null)
        {
            return false;
        }

        var automation = await automations.Detail(
            run.ProjectId,
            run.AutomationId,
            cancellationToken
        );
        return automation?.RequiresApproval ?? false;
    }

    async Task<AgentResult> Invoke(Run run, bool planning, CancellationToken cancellationToken)
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

        // Selection is composition (opencode-runtime D1): the Automation's runtime names the
        // implementation and its credential — which MAY be absent for free providers (D3).
        var selection = runtimes.For(automation.Runtime);
        if (selection is null)
        {
            return Failure($"No runtime named '{automation.Runtime}' is registered.");
        }

        string vendorToken;
        var aiKey = string.Empty;
        try
        {
            vendorToken = await secrets.Resolve(connector.SecretName, cancellationToken);
            if (selection.CredentialSecretName is { } credentialName)
            {
                aiKey = await secrets.Resolve(credentialName, cancellationToken);
            }
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

        // The catalogue ships whole (DEC-026); only this action executes yet, and saying so
        // beats a Run that silently does something else (agent-implements-pr spec).
        if (automation.Action != "ImplementToPullRequest")
        {
            return Failure(
                $"Action '{automation.Action}' is not executable yet — it is recorded and will "
                    + "run when its Agent lands."
            );
        }

        var coordinates = new CodeCoordinates(connector.Owner, connector.Repository);
        var prepared = await workspace.Prepare(coordinates, run.Id, vendorToken, cancellationToken);
        if (prepared.IsError)
        {
            // Stage-named refusal (design D4): the reason says "clone", not "something".
            return Failure(prepared.FirstError.Description);
        }

        try
        {
            // The Agent implements; the ceremony is ours (design D1). The prompt says so, or
            // the agent and the workspace seam would both try to own the same push.
            var context =
                $"Story #{story.VendorStoryId}: {story.Title}\n"
                + $"State: {story.State}; labels: {string.Join(", ", story.Labels)}.\n\n"
                + $"Description:\n{Requirement(story.Body)}";

            var prompt = planning
                ? "Read the repository at your current working directory and write a short "
                    + "implementation plan for the following story: the files you would change "
                    + "and why, in markdown. Change nothing — this is a proposal a human will "
                    + $"review before any code is written.\n\n{context}"
                // The approved Plan is an input (design D2): without it the human blessed a
                // document the Agent never sees again.
                : $"Implement the following story in the repository at your current working "
                    + $"directory.\n\n{context}\n\n"
                    + PlanSection(run.Plan)
                    + "Make the code changes only. Do not commit, push, or open pull requests — "
                    + "the orchestrator publishes your changes when you are done.";

            var agentResult = await selection.Runtime.Execute(
                new AgentInstruction(
                    prompt,
                    automation.Action,
                    automation.Timeout,
                    prepared.Value.Path,
                    new AgentCredentials(vendorToken, aiKey)
                ),
                cancellationToken
            );

            if (!agentResult.Succeeded || planning)
            {
                // Phase 1 publishes nothing: a plan-phase pull request would be a lie.
                return agentResult;
            }

            var published = await workspace.Publish(
                prepared.Value,
                $"feat: story #{story.VendorStoryId} — {story.Title}",
                $"Automated implementation of story #{story.VendorStoryId} "
                    + $"({connector.Owner}/{connector.Repository}) by run {run.Id}.",
                vendorToken,
                cancellationToken
            );

            return published.IsError
                ? agentResult with
                {
                    Succeeded = false,
                    Log = published.FirstError.Description,
                }
                : agentResult with
                {
                    OutputLink = published.Value.PullRequestUrl,
                };
        }
        finally
        {
            try
            {
                Directory.Delete(prepared.Value.Path, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp directory is not worth failing a finished Run over.
            }
        }
    }

    static AgentResult Failure(string log) =>
        new(Succeeded: false, Log: log, OutputLink: null, Usage: null);

    const int FailureLimit = 1000;

    /// <summary>Bounded like the prompt body, and for the same reason (story-detail D3).</summary>
    const int PlanLimit = 20000;

    static string Truncate(string text, int limit) => text.Length <= limit ? text : text[..limit];

    static string PlanSection(string? plan) =>
        string.IsNullOrWhiteSpace(plan)
            ? string.Empty
            : $"A human approved this plan — follow it:\n{plan}\n\n";

    /// <summary>
    /// The requirement, bounded at the prompt rather than at rest (design D3): the Mirror keeps
    /// the vendor's whole body, but an unbounded prompt is a cost and timeout surprise. The
    /// truncation says so, because an Agent silently given half a requirement will confidently
    /// implement half a story.
    /// </summary>
    const int PromptBodyLimit = 8000;

    static string Requirement(string? body) =>
        string.IsNullOrWhiteSpace(body) ? "(the story has no description)"
        : body.Length <= PromptBodyLimit ? body
        : body[..PromptBodyLimit] + "\n\n[description truncated by the orchestrator]";
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
        EventId = 3106,
        Level = LogLevel.Information,
        Message = "Run {RunId} produced a plan and is awaiting approval"
    )]
    public static partial void AwaitingApproval(ILogger logger, Guid runId);

    [LoggerMessage(
        EventId = 3105,
        Level = LogLevel.Error,
        Message = "Run {RunId} crashed during execution and was marked Failed"
    )]
    public static partial void Crashed(ILogger logger, Exception exception, Guid runId);
}
