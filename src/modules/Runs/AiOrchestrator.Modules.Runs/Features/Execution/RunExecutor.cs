using System.Text;
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
    ILocalCodeWorkspace localWorkspace,
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

        // A change-targeted Run never plans: the launch is the human intent (run-on-a-pr,
        // UC-012's reasoning), and there is no Automation to carry an approval flag anyway.
        if (run.AutomationId is not { } automationId)
        {
            return false;
        }

        var automation = await automations.Detail(run.ProjectId, automationId, cancellationToken);
        return automation?.RequiresApproval ?? false;
    }

    /// <summary>
    /// Hands work on (#115, a set since #165): applies every one of this Automation's output labels
    /// through UC-008's write, so each lands at the vendor, returns as an ordinary StoryChanged and
    /// is matched like any other label — nothing here knows what happens next. Returns null when
    /// there was nothing to hand on or everything landed, and otherwise a sentence naming what did
    /// not.
    /// <para>
    /// <b>Every label is attempted.</b> Stopping at the first refusal would apply an arbitrary prefix
    /// of the set and report one problem when there might be three — and #165's criterion is that a
    /// label the vendor could not ensure is *reported*, which a sentence about a different label is
    /// not. The consequence is real and visible on the Story: a Run that fails here may already have
    /// handed on through the labels that did land (design D2).
    /// </para>
    /// <para>
    /// The grill keeps its documented default here rather than in data (grill design D5): a
    /// product-wide default would silently chain every Automation an Admin created without
    /// thinking about it. An empty set is what "named nothing" means now.
    /// </para>
    /// </summary>
    async Task<string?> HandOn(Run run, CancellationToken cancellationToken)
    {
        // Labels are a Story concept: a change-targeted Run has nothing to hand on to (run-on-a-pr).
        if (run.AutomationId is not { } automationId || run.VendorStoryId is not { } vendorStoryId)
        {
            return null;
        }

        var automation = await automations.Detail(run.ProjectId, automationId, cancellationToken);

        // No default any more (#162): the grill was the one action that defaulted an output label,
        // and with the catalogue gone an Automation that names none hands nothing on. What it hands
        // on is now entirely what its Admin wired.
        var labels = automation?.OutputLabels ?? [];

        var refusals = new List<string>();

        foreach (var label in labels)
        {
            var refusal = await storyWriter.ApplyLabel(
                run.ProjectId,
                vendorStoryId,
                label,
                cancellationToken
            );

            if (refusal is not null)
            {
                // Named, not counted: which label failed is what tells the Admin whether the branch
                // they care about is the one that broke.
                refusals.Add($"'{label}': {refusal}");
            }
        }

        return refusals.Count == 0 ? null : string.Join(" ", refusals);
    }

    /// <summary>
    /// One action, one shape (#162): clone the project's repository with its credential, resolve the
    /// prompt the project itself wrote, run the agent, and record what came back.
    /// <para>
    /// <b>Nothing is published afterwards.</b> The orchestrator used to open the pull request, write
    /// the comment, transition the state and parse the estimate on the agent's behalf; all of that is
    /// gone. The agent holds the same credential and does those itself, or they do not happen
    /// (DEC-062).
    /// </para>
    /// <para>
    /// Two promises degraded with it, and the decision says so rather than the code pretending
    /// otherwise: a planning phase writing nothing, and a cancelled Run producing no pull request,
    /// are now what the prompt says it will do. Both were enforced by owning the write.
    /// </para>
    /// </summary>
    async Task<Outcome> Invoke(
        Run run,
        bool planning,
        Action<string> onOutput,
        CancellationToken cancellationToken
    )
    {
        // The target fork (run-on-a-pr, design D4): a change Run has no Story to read and no
        // Automation to load — its instruction is recorded on the Run, its runtime was named at
        // launch or defaults, and its branch is the change's own. Everything after this block —
        // credentials, transcript, usage, terminal states — is one path again.
        var targetsChange = run.TargetChangeNumber is not null;

        AutomationDetail? automation = null;
        StorySnapshot? story = null;

        if (!targetsChange)
        {
            story = await stories.Find(run.ProjectId, run.VendorStoryId!, cancellationToken);
            if (story is null)
            {
                return new Outcome(Failure("The mirrored story no longer exists."));
            }

            automation = await automations.Detail(
                run.ProjectId,
                run.AutomationId!.Value,
                cancellationToken
            );
            if (automation is null)
            {
                return new Outcome(Failure("The automation is no longer enabled on this project."));
            }
        }

        var connector = await connectors.Find(run.ProjectId, cancellationToken);
        if (connector is null)
        {
            return new Outcome(Failure("The project has no connector."));
        }

        // Selection is composition (opencode-runtime D1): the Automation's runtime names the
        // implementation and its credential — which MAY be absent for free providers (D3). A
        // change Run carries the name the launch chose, or the same default the form defaults to.
        var runtimeName = automation?.Runtime ?? run.RuntimeName ?? "ClaudeCodeHeadless";
        var selection = runtimes.For(runtimeName);
        if (selection is null)
        {
            return new Outcome(Failure($"No runtime named '{runtimeName}' is registered."));
        }

        var vendorToken = string.Empty;
        var aiKey = string.Empty;
        try
        {
            // A Local run never resolves the vendor credential (#210, design D5): the folder
            // may point at a different remote entirely, and the host's own tooling already
            // holds whatever identity its owner uses. The transcript states this below.
            if (run.Locus != RunLocus.Local)
            {
                vendorToken = await secrets.Resolve(connector.SecretName, cancellationToken);
            }
        }
        catch (SecretNotFoundException exception)
        {
            // The name that failed is safe to state; a value never appears (BR-010).
            return new Outcome(Failure($"Credential could not be resolved: {exception.Message}"));
        }

        // The AI credential fails with its own sentence (#279): unlike the vendor's, it has a
        // switched-off alternative — no name configured means the machine's own session — and
        // a failure that hides the alternative sends the operator hunting for a key they may
        // not need. Nothing retries (BR-004), so the failure carries the whole remedy.
        if (selection.CredentialSecretName is { } credentialName)
        {
            try
            {
                aiKey = await secrets.Resolve(credentialName, cancellationToken);
            }
            catch (SecretNotFoundException)
            {
                return new Outcome(
                    Failure(
                        "Credential could not be resolved: "
                            + AgentRuntimeRemedies.MissingAiCredential(credentialName, runtimeName)
                    )
                );
            }
        }

        string instruction;
        if (targetsChange)
        {
            // The Member's ad-hoc text is the prompt body (run-on-a-pr); the orchestrator adds
            // only the change's framing — which change, which branch — the way it only ever adds
            // a Story's framing. Pushing the result is the Agent's own act (DEC-062).
            instruction =
                $"{run.Instruction}\n\nYou are working on the open pull request "
                + $"#{run.TargetChangeNumber} (\"{run.TargetChangeTitle}\") of this repository. "
                + $"The workspace is already on its head branch '{run.TargetChangeBranch}'. "
                + "Commit your work and push it to that same branch; do not open a new pull "
                + "request and do not create a new branch.";
        }
        else
        {
            // One description, shared with the conversation path (#189, design D3) — so a prompt
            // tried in the scratchpad is tried against the input the Run will give it.
            var context = StoryDescription.Of(story!);

            // The prompt is read before the workspace, because both of its refusals must land
            // before any money is spent — cloning to discover a file is missing is spend for
            // nothing (design D4).
            var (body, refusal) = await RepositoryPrompt(
                run.ProjectId,
                automation!,
                cancellationToken
            );
            if (refusal is not null)
            {
                return new Outcome(Failure(refusal));
            }

            // What the orchestrator still says, and it is only ever framing: which story, which
            // phase, and the approved plan when there is one. What to *do* is entirely the
            // project's prompt.
            instruction = planning
                ? $"{body}\n\n{context}\n\nThis is a planning phase: a human will review what you "
                    + "produce before any work is carried out."
                : $"{body}\n\n{context}\n\n{PlanSection(run.Plan)}".TrimEnd();
        }

        // The workspace is the locus decision (#210, design D1): same queue, same worker, same
        // runtime — a Local run works in the Connector's folder, a Pod run in a fresh clone.
        if (run.Locus == RunLocus.Local)
        {
            if (targetsChange)
            {
                // Scoped out at grill: a local Run never pushes (BR-016's lane), and a change Run
                // whose work cannot reach the change is a promise the record would break.
                return new Outcome(
                    Failure(
                        "A change-targeted Run executes on the pod lane; the local lane never pushes."
                    )
                );
            }

            // Said where a reader will look for it: the transcript (BR-016's companion promise).
            onOutput(
                $"Running as a local process against '{connector.LocalPath}' — the host's own "
                    + "credentials; no vendor token was resolved for this run."
            );

            var branch = $"ai/{run.VendorStoryId}-{Slug(story!.Title)}";
            var local = await localWorkspace.Prepare(
                connector.LocalPath!,
                branch,
                cancellationToken
            );
            if (local.IsError)
            {
                return new Outcome(Failure(local.FirstError.Description));
            }

            // Recorded when the workspace actually exists, never predicted — and saved now,
            // not with the terminal state: the outer flow reloads the row to give a human's
            // cancellation the last word (design D3), and an unsaved fact would not survive
            // that reload. A crash mid-run keeps the audit too (BR-014).
            run.RecordLocalExecution(connector.LocalPath!, branch);
            await database.SaveChangesAsync(cancellationToken);

            var localResult = await selection.Runtime.Execute(
                new AgentInstruction(
                    instruction,
                    automation!.Action,
                    automation.Timeout,
                    local.Value.Path,
                    new AgentCredentials(vendorToken, aiKey),
                    onOutput
                ),
                cancellationToken
            );

            // Success leaves the branch checked out — it IS the output; failure restores the
            // owner's checkout. A commit that fails turns a claimed success into the truth.
            var concluded = await localWorkspace.Conclude(
                local.Value,
                $"ai: {story!.Title}",
                localResult.Succeeded,
                cancellationToken
            );
            if (localResult.Succeeded && concluded.IsError)
            {
                return new Outcome(Failure(concluded.FirstError.Description));
            }

            return new Outcome(localResult);
        }

        // A change Run is prepared on the change's own head branch — the named-branch checkout
        // the install path already performs — instead of cutting a fresh run/<id> branch. The
        // push stays the Agent's own act either way (DEC-062).
        var coordinates = new CodeCoordinates(connector.Owner, connector.Repository);
        var prepared = targetsChange
            ? await workspace.Prepare(
                coordinates,
                run.TargetChangeBranch!,
                vendorToken,
                cancellationToken
            )
            : await workspace.Prepare(coordinates, run.Id, vendorToken, cancellationToken);
        if (prepared.IsError)
        {
            // Stage-named refusal (design D4): the reason says "clone", not "something".
            return new Outcome(Failure(prepared.FirstError.Description));
        }

        var agentResult = await selection.Runtime.Execute(
            new AgentInstruction(
                instruction,
                automation?.Action ?? "ChangeInstruction",
                automation?.Timeout ?? DefaultChangeTimeout,
                prepared.Value.Path,
                new AgentCredentials(vendorToken, aiKey),
                onOutput
            ),
            cancellationToken
        );

        // Straight back. There is no publish step to withhold and no answer to post — which is the
        // whole of #162, and the reason the second cancellation boundary went with it: there is no
        // longer a consequence sitting between the spend and the record.
        return new Outcome(agentResult);
    }

    /// <summary>
    /// BR-005's default, for the Run kind that has no Automation to carry a configured one
    /// (run-on-a-pr): the same thirty minutes an Automation gets when its Admin leaves the
    /// field blank.
    /// </summary>
    static readonly TimeSpan DefaultChangeTimeout = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The story's title as a branch-safe fragment: lowercase alphanumerics and dashes, bounded.
    /// Deterministic on purpose — re-running the same story overwrites nothing (each branch also
    /// carries the story id) but reads as the same work.
    /// </summary>
    static string Slug(string title)
    {
        var builder = new StringBuilder(capacity: 40);
        foreach (var character in title.ToLowerInvariant())
        {
            if (builder.Length >= 40)
            {
                break;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-') is { Length: > 0 } slug ? slug : "story";
    }

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
            automation.PromptPath ?? string.Empty,
            cancellationToken
        );

        if (document.Failure is not null)
        {
            return (null, document.Failure);
        }

        var body = PromptText.WithoutFrontmatter(document.Content ?? string.Empty);

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
