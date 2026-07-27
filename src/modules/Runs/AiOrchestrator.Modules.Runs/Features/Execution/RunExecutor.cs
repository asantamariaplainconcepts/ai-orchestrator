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
    IConversationReader conversations,
    Conversation.ConversationGate conversationGate,
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
            var outcome = await Invoke(run, planning, cancellationToken);
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

    async Task<Outcome> Invoke(Run run, bool planning, CancellationToken cancellationToken)
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

        // The grill converses rather than writing once, so it routes before the simple
        // actions (#79, DEC-048).
        if (automation.Action == "GrillToReady")
        {
            return await RunGrill(
                run,
                automation,
                context,
                selection,
                vendorToken,
                aiKey,
                cancellationToken
            );
        }

        // Every other catalogue action is one shot. Only implement-to-PR touches code, so only
        // it prepares a workspace — the rest are one prompt and one vendor write.
        if (automation.Action != "ImplementToPullRequest")
        {
            return new Outcome(
                await RunSimpleAction(
                    run,
                    automation,
                    story,
                    context,
                    selection,
                    vendorToken,
                    aiKey,
                    cancellationToken
                )
            );
        }

        var coordinates = new CodeCoordinates(connector.Owner, connector.Repository);
        var prepared = await workspace.Prepare(coordinates, run.Id, vendorToken, cancellationToken);
        if (prepared.IsError)
        {
            // Stage-named refusal (design D4): the reason says "clone", not "something".
            return new Outcome(Failure(prepared.FirstError.Description));
        }

        try
        {
            // The Agent implements; the ceremony is ours (design D1). The prompt says so, or
            // the agent and the workspace seam would both try to own the same push.
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
                return new Outcome(agentResult);
            }

            // Boundary two (design D2): the spend is sunk, but the *consequence* is not. This
            // check has to live here, immediately before Publish — one level up, after Invoke
            // returns, the pull request already exists.
            await database.Entry(run).ReloadAsync(cancellationToken);
            if (run.IsCancelled)
            {
                return new Outcome(
                    agentResult with
                    {
                        Succeeded = false,
                        Log = "Cancelled before publishing.",
                    }
                );
            }

            var published = await workspace.Publish(
                prepared.Value,
                $"feat: story #{story.VendorStoryId} — {story.Title}",
                $"Automated implementation of story #{story.VendorStoryId} "
                    + $"({connector.Owner}/{connector.Repository}) by run {run.Id}.",
                vendorToken,
                cancellationToken
            );

            return new Outcome(
                published.IsError
                    ? agentResult with
                    {
                        Succeeded = false,
                        Log = published.FirstError.Description,
                    }
                    : agentResult with
                    {
                        OutputLink = published.Value.PullRequestUrl,
                    }
            );
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

    /// <summary>The framework's conventions; an Automation may override both (grill D5).</summary>
    internal const string DefaultRubricPath = "docs/process/definition-of-ready.md";

    internal const string DefaultReadyLabel = "ready-for-proposal";

    /// <summary>
    /// UC-024: interrogate the Story to its project's readiness bar. Each pass is stateless —
    /// the rubric, the body and the whole conversation are re-read (grill D3) — and ends one of
    /// three ways: READY (label + verdict, the Run succeeds), questions (the Run waits, #78),
    /// or an honest failure. The rubric is read before anything is written (D2): a grill that
    /// cannot find its bar must not put that confusion on somebody's backlog.
    /// </summary>
    async Task<Outcome> RunGrill(
        Run run,
        AutomationDetail automation,
        string context,
        AgentRuntimeSelection selection,
        string vendorToken,
        string aiKey,
        CancellationToken cancellationToken
    )
    {
        var rubricPath = automation.RubricPath ?? DefaultRubricPath;

        var rubric = await documents.Read(run.ProjectId, rubricPath, cancellationToken);
        if (rubric.Failure is not null)
        {
            return new Outcome(
                Failure(
                    $"The readiness document could not be read at '{rubricPath}': {rubric.Failure}"
                )
            );
        }

        var conversation = await conversations.ReadSince(
            run.ProjectId,
            run.VendorStoryId,
            run.CreatedAt,
            cancellationToken
        );
        if (conversation.Failure is not null)
        {
            return new Outcome(
                Failure($"The conversation could not be read: {conversation.Failure}")
            );
        }

        var dialogue = string.Join(
            "\n\n",
            conversation.Comments.Select(comment =>
                Conversation.RunMarker.IsAgentComment(comment.Body)
                    ? $"You previously asked:\n{StripMarker(comment.Body)}"
                    : $"The human answered:\n{comment.Body}"
            )
        );

        var prompt =
            "You are assessing whether a story meets its team's Definition of Ready, quoted "
            + "below. If every criterion is met, reply with the single word READY on the first "
            + "line, followed by a short verdict naming the criteria that pass. If anything is "
            + "missing, reply ONLY with the specific questions whose answers would close the "
            + "gaps — name the missing criteria, never ask generically, and never repeat a "
            + "question the conversation below has already answered.\n\n"
            + $"Definition of Ready:\n{rubric.Content}\n\n{context}"
            + (dialogue.Length > 0 ? $"\n\nConversation so far:\n{dialogue}" : string.Empty);

        var workspacePath = Directory.CreateTempSubdirectory("grill-").FullName;
        AgentResult agentResult;
        try
        {
            agentResult = await selection.Runtime.Execute(
                new AgentInstruction(
                    prompt,
                    automation.Action,
                    automation.Timeout,
                    workspacePath,
                    new AgentCredentials(vendorToken, aiKey)
                ),
                cancellationToken
            );
        }
        finally
        {
            try
            {
                Directory.Delete(workspacePath, recursive: true);
            }
            catch (IOException) { }
        }

        if (!agentResult.Succeeded)
        {
            return new Outcome(agentResult);
        }

        var answer = agentResult.Log.Trim();

        if (!FirstWord(answer).Equals("READY", StringComparison.OrdinalIgnoreCase))
        {
            // Not ready: the whole reply is the questions. A rambling model degrades into
            // questions a human reads — never into a wrong state (grill D1).
            return new Outcome(agentResult, Questions: answer);
        }

        var readyLabel = automation.ReadyLabel ?? DefaultReadyLabel;

        // The label rides UC-008's write path, so it lands at the vendor, returns as an
        // ordinary StoryChanged, and can trigger the next Automation (grill D4).
        var labelled = await storyWriter.ApplyLabel(
            run.ProjectId,
            run.VendorStoryId,
            readyLabel,
            cancellationToken
        );
        if (labelled is not null)
        {
            return new Outcome(agentResult with { Succeeded = false, Log = labelled });
        }

        var verdict =
            answer.Length > "READY".Length
                ? answer["READY".Length..].Trim()
                : "The story meets its Definition of Ready.";
        var commented = await storyWriter.AddComment(
            run.ProjectId,
            run.VendorStoryId,
            Conversation.RunMarker.Sign(run.Id, verdict),
            cancellationToken
        );

        return commented is not null
            ? new Outcome(agentResult with { Succeeded = false, Log = commented })
            : new Outcome(agentResult);
    }

    static string StripMarker(string body)
    {
        var lines = body.Split('\n');
        return string.Join('\n', lines.Skip(1)).Trim();
    }

    /// <summary>
    /// The three actions that touch no code (design D1): one prompt, one answer, one vendor
    /// write. No workspace is prepared, which is also why they are fast and cheap.
    /// </summary>
    async Task<AgentResult> RunSimpleAction(
        Run run,
        AutomationDetail automation,
        StorySnapshot story,
        string context,
        AgentRuntimeSelection selection,
        string vendorToken,
        string aiKey,
        CancellationToken cancellationToken
    )
    {
        var instruction = automation.Action switch
        {
            "RefineOrComment" =>
                "Analyse the following story and reply with refinement questions, analysis, or "
                    + "a draft of its acceptance criteria. Your whole reply becomes a comment on "
                    + $"the story, so write it for its readers.\n\n{context}",
            "TransitionState" =>
                "Decide what state the following story should be in and reply with ONLY that "
                    + $"state as a single word.\n\n{context}",
            "Estimate" =>
                "Estimate the following story in story points and reply with the number first, "
                    + $"then one short paragraph explaining it.\n\n{context}",
            _ => string.Empty,
        };

        if (instruction.Length == 0)
        {
            return Failure($"Action '{automation.Action}' has no implementation.");
        }

        // A temporary directory only because the runtime needs somewhere to be; nothing is
        // cloned into it and nothing is published from it.
        var workspacePath = Directory.CreateTempSubdirectory("action-").FullName;

        AgentResult agentResult;
        try
        {
            agentResult = await selection.Runtime.Execute(
                new AgentInstruction(
                    instruction,
                    automation.Action,
                    automation.Timeout,
                    workspacePath,
                    new AgentCredentials(vendorToken, aiKey)
                ),
                cancellationToken
            );
        }
        finally
        {
            try
            {
                Directory.Delete(workspacePath, recursive: true);
            }
            catch (IOException) { }
        }

        if (!agentResult.Succeeded)
        {
            return agentResult;
        }

        var answer = agentResult.Log.Trim();

        var failure = automation.Action switch
        {
            "RefineOrComment" => await storyWriter.AddComment(
                run.ProjectId,
                run.VendorStoryId,
                answer,
                cancellationToken
            ),
            "TransitionState" => await storyWriter.SetState(
                run.ProjectId,
                run.VendorStoryId,
                FirstWord(answer),
                cancellationToken
            ),
            "Estimate" => await Estimate(run, answer, cancellationToken),
            _ => "Unreachable.",
        };

        return failure is null
            ? agentResult
            : agentResult with
            {
                Succeeded = false,
                Log = failure,
            };
    }

    async Task<string?> Estimate(Run run, string answer, CancellationToken cancellationToken)
    {
        // The number is the estimate; a reply with none is a stated failure, never a guessed
        // zero — an invented estimate is worse than no estimate (design D2).
        var digits = new string([.. answer.TakeWhile(char.IsDigit)]);
        if (digits.Length == 0 || !int.TryParse(digits, out var points))
        {
            return $"The agent's answer did not start with a number: '{Truncate(answer, 200)}'.";
        }

        var labelled = await storyWriter.SetEstimate(
            run.ProjectId,
            run.VendorStoryId,
            points,
            cancellationToken
        );

        return labelled
            // UC-019 asks for the field AND the reasoning.
            ?? await storyWriter.AddComment(
                run.ProjectId,
                run.VendorStoryId,
                answer,
                cancellationToken
            );
    }

    static string FirstWord(string answer) =>
        answer
            .Split([' ', '\n', '\r', '.', ','], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
        ?? answer;

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
