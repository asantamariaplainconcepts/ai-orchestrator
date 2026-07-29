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
    IStoryWriter storyWriter,
    IDocumentReader documents,
    Conversation.ConversationGate conversationGate,
    ISecretResolver secrets,
    IAgentRuntimeSelector runtimes,
    ICodeWorkspace workspace,
    Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopes,
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

        if (run.IsCancelled)
        {
            // Boundary one (run-cancellation D2): nothing has been spent yet, so stopping here
            // costs nothing and the human's decision stands.
            ExecutionLog.CancelledBeforeStart(logger, runId);
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
            // The live window (#96): every runtime line becomes a committed chunk while the
            // Run executes. Disposed after Invoke so the tail flushes before the terminal
            // state is saved — a watcher never sees "Succeeded" with half a log.
            Outcome outcome;
            await using (var logWriter = new RunLogWriter(run.Id, scopes, logger))
            {
                outcome = await Invoke(run, planning, logWriter.Write, cancellationToken);
            }
            var result = outcome.Result;

            // The human always wins the race (design D3): a cancellation that landed while the
            // agent worked must not be overwritten by the outcome that arrived afterwards.
            await database.Entry(run).ReloadAsync(cancellationToken);
            if (run.IsCancelled)
            {
                ExecutionLog.CancelledDuringRun(logger, runId);
                return;
            }

            if (outcome.Questions is { } questions && result.Succeeded)
            {
                // The grill's ask, deliberately after the cancellation boundary: a cancelled Run
                // must not put questions on somebody's Story (#78/#79).
                var delivery = await conversationGate.AskAndWait(
                    run,
                    questions,
                    clock.GetUtcNow(),
                    cancellationToken
                );
                if (delivery is not null)
                {
                    // A Run must never wait on questions nobody can read.
                    run.Fail(clock.GetUtcNow(), Truncate(delivery, FailureLimit));
                    ExecutionLog.Failed(logger, runId);
                }
                else
                {
                    ExecutionLog.AwaitingInput(logger, runId);
                }
            }
            else if (planning && result.Succeeded)
            {
                // BR-006: the wait is untimed and holds no cap slot — it is not work.
                run.AwaitApproval(clock.GetUtcNow(), Truncate(result.Log, PlanLimit));
                ExecutionLog.AwaitingApproval(logger, runId);
            }
            else if (result.Succeeded)
            {
                // The one place work is handed on (#115, design D2): every action, not just the
                // grill, and only here — a chain claims the previous step worked, and BR-004
                // makes a failed Run terminal until a human intervenes.
                var handOff = await HandOn(run, cancellationToken);
                if (handOff is not null)
                {
                    // The label is the deliverable of a chaining Automation; if it did not land,
                    // the Run did not do its job, and saying otherwise would strand the chain
                    // silently.
                    run.Fail(clock.GetUtcNow(), Truncate(handOff, FailureLimit));
                    ExecutionLog.Failed(logger, runId);
                }
                else
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

    /// <summary>
    /// Hands work to the next Automation (#115): applies this Automation's output label through
    /// UC-008's write, so it lands at the vendor, returns as an ordinary StoryChanged and is
    /// matched like any other label — nothing here knows what happens next. Returns null when
    /// there is nothing to hand on or the write succeeded, and the refusal sentence otherwise.
    /// <para>
    /// This survives #162 deliberately. The writes that change removed are the ones that completed the
    /// agent's work — publishing its branch, posting its reply, turning its answer into a state. The
    /// hand-off label is not that: it is the product executing configuration it declared itself
    /// (DEC-053's workflow, as against the catalogue), which the prompt can neither request nor
    /// suppress. It is also the one output-label write path, and the only one.
    /// </para>
    /// </summary>
    async Task<string?> HandOn(Run run, CancellationToken cancellationToken)
    {
        var automation = await automations.Detail(
            run.ProjectId,
            run.AutomationId,
            cancellationToken
        );

        // No per-action default any more: the grill's implicit ready label went with the catalogue, and
        // an Automation now hands on only what its Admin named.
        var label = automation?.OutputLabel;

        return string.IsNullOrWhiteSpace(label)
            ? null
            : await storyWriter.ApplyLabel(
                run.ProjectId,
                run.VendorStoryId,
                label,
                cancellationToken
            );
    }

    async Task<Outcome> Invoke(
        Run run,
        bool planning,
        Action<string> onOutput,
        CancellationToken cancellationToken
    )
    {
        var story = await stories.Find(run.ProjectId, run.VendorStoryId, cancellationToken);
        if (story is null)
        {
            return new Outcome(
                new AgentResult(
                    Succeeded: false,
                    Log: "The mirrored story no longer exists.",
                    OutputLink: null,
                    Usage: null
                )
            );
        }

        var automation = await automations.Detail(
            run.ProjectId,
            run.AutomationId,
            cancellationToken
        );
        if (automation is null)
        {
            return new Outcome(
                new AgentResult(
                    Succeeded: false,
                    Log: "The automation is no longer enabled on this project.",
                    OutputLink: null,
                    Usage: null
                )
            );
        }

        var connector = await connectors.Find(run.ProjectId, cancellationToken);
        if (connector is null)
        {
            return new Outcome(
                new AgentResult(
                    Succeeded: false,
                    Log: "The project has no connector.",
                    OutputLink: null,
                    Usage: null
                )
            );
        }

        // Selection is composition (opencode-runtime D1): the Automation's runtime names the
        // implementation and its credential — which MAY be absent for free providers (D3).
        var selection = runtimes.For(automation.Runtime);
        if (selection is null)
        {
            return new Outcome(Failure($"No runtime named '{automation.Runtime}' is registered."));
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
            return new Outcome(
                new AgentResult(
                    Succeeded: false,
                    Log: $"Credential could not be resolved: {exception.Message}",
                    OutputLink: null,
                    Usage: null
                )
            );
        }

        var context =
            $"Story #{story.VendorStoryId}: {story.Title}\n"
            + $"State: {story.State}; labels: {string.Join(", ", story.Labels)}.\n\n"
            + $"Description:\n{Requirement(story.Body)}";

        // One action, one path (#162). There is no branch on what the Automation does, because there is
        // no vocabulary left to branch on: the prompt in the project's repository decides, and the agent
        // holds the credential and the workspace to act on it.
        var (body, refusal) = await RepositoryPrompt(run.ProjectId, automation, cancellationToken);
        if (refusal is not null)
        {
            // Still before any spend, and still naming the resolved path (#150, design D4).
            return new Outcome(Failure(refusal));
        }

        var coordinates = new CodeCoordinates(connector.Owner, connector.Repository);
        var prepared = await workspace.Prepare(coordinates, run.Id, vendorToken, cancellationToken);
        if (prepared.IsError)
        {
            // Stage-named refusal: the reason says "clone", not "something".
            return new Outcome(Failure(prepared.FirstError.Description));
        }

        try
        {
            // BR-007's two phases still route, and what each phase means is now the prompt's promise
            // rather than this executor's guarantee (design D5). The planning pass says so in words
            // because words are all that is left to say it with — the orchestrator no longer holds the
            // workspace back from being published, so it cannot enforce containment.
            var instruction = planning
                ? $"{body}\n\n{context}\n\nThis is a planning pass: write a short plan of what you "
                    + "would do and why, and change nothing. A human reviews it before you act."
                : $"{body}\n\n{context}{PlanSection(run.Plan)}";

            var agentResult = await selection.Runtime.Execute(
                new AgentInstruction(
                    instruction,
                    automation.Action,
                    automation.Timeout,
                    prepared.Value.Path,
                    new AgentCredentials(vendorToken, aiKey),
                    onOutput
                ),
                cancellationToken
            );

            // Nothing follows. The agent's result is the Run's outcome, and the orchestrator publishes
            // none of the action's own writes — no pull request, no comment, no state, no estimate.
            // The Automation's output label is still written, one level up in HandOn: that is the
            // workflow the product configured (DEC-053), not the completion of the agent's work.
            return new Outcome(agentResult);
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

    internal const string DefaultRubricPath = "docs/process/definition-of-ready.md";

    /// <summary>
    /// The project's own prompt (#150): read live, frontmatter dropped, and refused before the agent
    /// if there is nothing to send. Both refusals name the <b>resolved</b> path, so a misconfigured
    /// prompts directory gives itself away instead of looking like a missing file (design D6).
    /// </summary>
    async Task<(string? Body, string? Failure)> RepositoryPrompt(
        Guid projectId,
        AutomationDetail automation,
        CancellationToken cancellationToken
    )
    {
        var document = await documents.ReadPrompt(
            projectId,
            automation.RubricPath ?? string.Empty,
            cancellationToken
        );

        if (document.Failure is not null)
        {
            return (null, document.Failure);
        }

        var body = StripFrontmatter(document.Content ?? string.Empty);

        // An empty prompt is a configuration mistake, not an instruction. Sending it would spend a
        // pass to ask an agent nothing, and no fallback is substituted: an Automation told to run the
        // repository's prompt either runs it or stops (design D4).
        return body.Length == 0
            ? (
                null,
                $"The prompt at '{document.ResolvedPath}' has no body once its frontmatter is removed."
            )
            : (body, null);
    }

    /// <summary>
    /// Drops a leading YAML frontmatter block. That block is how <i>another</i> runner is told what to
    /// do with the file, and this product's wiring is the Automation — so honouring a
    /// <c>model:</c> line would let a file in somebody's repository choose what this product spends,
    /// and a <c>tools:</c> line would let it grant itself powers the Automation withheld.
    /// </summary>
    internal static string StripFrontmatter(string content)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var opening = Array.FindIndex(lines, line => line.Trim().Length > 0);

        if (opening < 0 || lines[opening].Trim() != "---")
        {
            return content.Trim();
        }

        for (var index = opening + 1; index < lines.Length; index++)
        {
            if (lines[index].Trim() is "---" or "...")
            {
                return string.Join('\n', lines.Skip(index + 1)).Trim();
            }
        }

        // An opening delimiter that never closes is not frontmatter. Treating it as such would
        // swallow the entire file and then refuse it as empty — a confusing lie about a file whose
        // real problem is a missing '---'.
        return content.Trim();
    }

    static string StripMarker(string body)
    {
        var lines = body.Split('\n');
        return string.Join('\n', lines.Skip(1)).Trim();
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

    [LoggerMessage(
        EventId = 3107,
        Level = LogLevel.Information,
        Message = "Run {RunId} was cancelled before its runtime was invoked — nothing was spent"
    )]
    public static partial void CancelledBeforeStart(ILogger logger, Guid runId);

    [LoggerMessage(
        EventId = 3108,
        Level = LogLevel.Information,
        Message = "Run {RunId} was cancelled during its invocation — the result is discarded and nothing is published"
    )]
    public static partial void CancelledDuringRun(ILogger logger, Guid runId);

    [LoggerMessage(EventId = 3104, Level = LogLevel.Warning, Message = "Run {RunId} failed")]
    public static partial void Failed(ILogger logger, Guid runId);

    [LoggerMessage(
        EventId = 3106,
        Level = LogLevel.Information,
        Message = "Run {RunId} produced a plan and is awaiting approval"
    )]
    public static partial void AwaitingApproval(ILogger logger, Guid runId);

    [LoggerMessage(
        EventId = 6110,
        Level = LogLevel.Information,
        Message = "Run {RunId} asked its questions and awaits input"
    )]
    public static partial void AwaitingInput(ILogger logger, Guid runId);

    [LoggerMessage(
        EventId = 3105,
        Level = LogLevel.Error,
        Message = "Run {RunId} crashed during execution and was marked Failed"
    )]
    public static partial void Crashed(ILogger logger, Exception exception, Guid runId);
}

/// <summary>
/// What one invocation produced: the agent's result, and — grill only — the questions whose
/// posting and wait must happen after the cancellation boundary in <see cref="RunExecutor.Execute"/>.
/// </summary>
sealed record Outcome(AgentResult Result, string? Questions = null);
