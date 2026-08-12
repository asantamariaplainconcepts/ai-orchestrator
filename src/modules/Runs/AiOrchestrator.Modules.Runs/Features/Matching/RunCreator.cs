using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Dispatch;
using AiOrchestrator.BuildingBlocks.Domain;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Projects.Contracts;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AiOrchestrator.Modules.Runs.Features.Matching;

/// <summary>
/// The one Run-creation path (BR-013's structural consequence, design D1 of run-now): event
/// matching and Run now both come through here, so BR-001 and BR-002 are enforced by exactly
/// one piece of code. Both lanes create the same Run — BR-007 splits at <i>execution</i>
/// (approval-gate D1), not at creation. The outcome is data; each caller chooses its
/// voice — the event handler stays silent where at-least-once makes silence correct, the
/// endpoint answers the human.
/// </summary>
sealed class RunCreator(
    RunsDbContext database,
    IRunDispatcher dispatcher,
    IProjectCatalog projects,
    IStoryReader stories,
    IConnectorReader connectors,
    ILocalCodeWorkspace localWorkspace,
    RunsOptions options,
    TimeProvider clock,
    IConfiguration configuration,
    ILogger<RunCreator> logger
)
{
    public async Task<RunCreation> Create(
        Guid projectId,
        string vendorStoryId,
        AutomationTrigger automation,
        CancellationToken cancellationToken,
        RunLocus? requestedLocus = null,
        string? runtimeName = null,
        /// <summary>The human's model for this Run only (#291); null records the resolution.</summary>
        string? model = null
    )
    {
        // An archived Project starts no work (#121). Checked here because this is the one
        // creation path both matching and Run now share, so neither can forget it — and asked
        // per creation rather than cached, since a Project is archived while this runs.
        if (!await projects.AcceptsWork(projectId, cancellationToken))
        {
            return new RunCreation.ProjectArchived();
        }

        // BR-007, before anything else is read or written: a held Story starts nothing (DEC-067).
        //
        // Here rather than in the matching handler, for the same reason the archived check above
        // is here — this is the one path both matching and Run now take, so neither can forget it.
        // Checking in the handler would leave Run now able to dispatch a held Story by hand, and
        // BR-013 is explicit that manual dispatch bypasses detection, never a rule.
        //
        // Read live rather than from the event: what matters is the labels the Story carries *now*
        // (BR-015), so a hold applied between the event and this moment is still honoured. A Story
        // the mirror cannot find is not held — it is absent, which the paths below answer in their
        // own voice.
        var story = await stories.Find(projectId, vendorStoryId, cancellationToken);
        if (StoryHold.IsHeld(story?.Labels))
        {
            return new RunCreation.Held();
        }

        // Where this Run will execute (#210): the project's code source decides the default,
        // and an explicit choice is honoured only when it is physically possible — a sandbox
        // cannot see the host's disk, and a folder cannot be cloned from a vendor.
        var connector = await connectors.Find(projectId, cancellationToken);
        var sourceIsLocal = string.Equals(
            connector?.CodeSource,
            "LocalFolder",
            StringComparison.Ordinal
        );
        var locus = requestedLocus ?? (sourceIsLocal ? RunLocus.Local : RunLocus.Sandbox);

        if (locus == RunLocus.Local && !sourceIsLocal)
        {
            return Refused(
                "This project's code source is a repository — a local run needs a folder on "
                    + "this machine, configured on the Connector."
            );
        }
        if (locus == RunLocus.Sandbox && sourceIsLocal)
        {
            return Refused(
                "This project's code is a folder on this machine, which an Agent in a sandbox cannot "
                    + "see — runs of this project execute locally."
            );
        }

        // The habitat's own declaration (#247): a Connector stored before the declaration — or
        // around the portal — still cannot produce a Local Run where the composition says the
        // folder is unreachable. The declared sentence, never a container path error later.
        if (
            locus == RunLocus.Local
            && IdentityHabitat.LocalFolderUnavailableReason(configuration) is { } declared
        )
        {
            return Refused(declared);
        }

        // BR-016, refused before any write (the BR-001 pattern): a Local run needs a repository to
        // cut its checkout from, and letting the Run exist first would spend a slot on a refusal.
        //
        // The clean-tree half of this check is gone (#331). The Run works in its own worktree, so
        // uncommitted work in the folder collides with nothing — and a refusal that survived here
        // would go on rejecting Runs the product is now perfectly able to execute.
        if (locus == RunLocus.Local)
        {
            var inspection = await localWorkspace.Inspect(connector!.LocalPath!, cancellationToken);
            if (!inspection.IsGitRepository)
            {
                return Refused(
                    $"'{connector.LocalPath}' is not a git repository — a local run needs one "
                        + "to branch in."
                );
            }
        }

        // Logged here, once, because matching deliberately discards outcomes: a labelled Story
        // that never runs must leave a trace somewhere a person can find.
        RunCreation.PreconditionFailed Refused(string reason)
        {
            MatchingLog.PreconditionRefused(logger, projectId, vendorStoryId, reason);
            return new RunCreation.PreconditionFailed(reason);
        }

        // BR-001 pre-check keeps the common case quiet; the index owns the race below. The
        // state list is the SAME array the index filter is generated from — hand-copying it
        // is what let a terminal state hold a Story hostage twice.
        var hasActiveRun = await database.Runs.AnyAsync(
            run =>
                run.ProjectId == projectId
                && run.VendorStoryId == vendorStoryId
                && RunStates.Active.Contains(run.State),
            cancellationToken
        );
        if (hasActiveRun)
        {
            return new RunCreation.AlreadyActive();
        }

        // BR-002, creation-side: at the cap the Run waits Queued and nothing is enqueued.
        var busy = await database.Runs.CountAsync(
            run =>
                run.ProjectId == projectId
                && (run.State == RunState.Planning || run.State == RunState.Executing),
            cancellationToken
        );
        var belowCap = busy < options.ProjectConcurrencyCap;

        // The human's per-Run choice, recorded at creation (#244, BR-014). Matching passes
        // null: a label-triggered Run involves no human and offers no override.
        var run = Run.Create(
            projectId,
            vendorStoryId,
            automation.AutomationId,
            locus,
            clock.GetUtcNow(),
            runtimeName,
            model
        );
        database.Runs.Add(run);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateActiveRun(exception))
        {
            // A concurrent creation for the same Story won the insert. BR-001 says ignored,
            // not queued — the loser creates nothing and enqueues nothing.
            return new RunCreation.AlreadyActive();
        }

        if (!belowCap)
        {
            MatchingLog.QueuedAtCap(logger, run.Id, projectId);
            return new RunCreation.QueuedAtCap(run.Id);
        }

        try
        {
            await dispatcher.Dispatch(run.Id, cancellationToken);
        }
        catch (Exception exception)
        {
            // The crash window, logged loudly rather than retried: a retry of the caller would
            // find the active Run and stop, so propagating buys nothing. The Run stays Queued
            // and visible; Run now (BR-013) is the recovery.
            MatchingLog.DispatchFailed(logger, exception, run.Id);
            return new RunCreation.DispatchFailed(run.Id);
        }

        run.MarkDispatched(clock.GetUtcNow());
        await database.SaveChangesAsync(cancellationToken);
        return new RunCreation.Dispatched(run.Id);
    }

    /// <summary>
    /// The change-targeted creation path (run-on-a-pr): shares every shared guard — accepts-work,
    /// the concurrency cap, dispatch, MarkDispatched — with its own BR-001-analogue trio, so a
    /// change Run obeys every rule a story Run does. Sandbox lane only: a local Run never pushes, and
    /// a change Run whose work cannot reach the change would break the record's promise.
    /// </summary>
    public async Task<RunCreation> CreateForChange(
        Guid projectId,
        int changeNumber,
        string changeUrl,
        string changeTitle,
        string changeBranch,
        string instruction,
        string? runtimeName,
        string? model,
        CancellationToken cancellationToken
    )
    {
        if (!await projects.AcceptsWork(projectId, cancellationToken))
        {
            return new RunCreation.ProjectArchived();
        }

        var connector = await connectors.Find(projectId, cancellationToken);
        if (string.Equals(connector?.CodeSource, "LocalFolder", StringComparison.Ordinal))
        {
            var refusal =
                "This project's code is a folder on this machine, and a local run never pushes — "
                + "a change-targeted Run executes on the sandbox lane.";
            MatchingLog.PreconditionRefused(logger, projectId, $"change #{changeNumber}", refusal);
            return new RunCreation.PreconditionFailed(refusal);
        }

        // The BR-001-analogue pre-check; the filtered unique index owns the race below, and the
        // state list is the same generated array, so the two cannot drift.
        var hasActiveRun = await database.Runs.AnyAsync(
            run =>
                run.ProjectId == projectId
                && run.TargetChangeNumber == changeNumber
                && RunStates.Active.Contains(run.State),
            cancellationToken
        );
        if (hasActiveRun)
        {
            return new RunCreation.AlreadyActive();
        }

        var busy = await database.Runs.CountAsync(
            run =>
                run.ProjectId == projectId
                && (run.State == RunState.Planning || run.State == RunState.Executing),
            cancellationToken
        );
        var belowCap = busy < options.ProjectConcurrencyCap;

        var run = Run.CreateForChange(
            projectId,
            changeNumber,
            changeUrl,
            changeTitle,
            changeBranch,
            instruction,
            runtimeName,
            RunLocus.Sandbox,
            clock.GetUtcNow(),
            model
        );
        database.Runs.Add(run);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateActiveRun(exception))
        {
            return new RunCreation.AlreadyActive();
        }

        if (!belowCap)
        {
            MatchingLog.QueuedAtCap(logger, run.Id, projectId);
            return new RunCreation.QueuedAtCap(run.Id);
        }

        try
        {
            await dispatcher.Dispatch(run.Id, cancellationToken);
        }
        catch (Exception exception)
        {
            MatchingLog.DispatchFailed(logger, exception, run.Id);
            return new RunCreation.DispatchFailed(run.Id);
        }

        run.MarkDispatched(clock.GetUtcNow());
        await database.SaveChangesAsync(cancellationToken);
        return new RunCreation.Dispatched(run.Id);
    }

    // Narrow on purpose, same as the Backlog reconciler: only a unique-key violation means
    // "someone else already did this".
    static bool IsDuplicateActiveRun(DbUpdateException exception) =>
        exception.InnerException
            is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

/// <summary>What creating a Run came to. Data, not exceptions — both callers pattern-match.</summary>
abstract record RunCreation
{
    RunCreation() { }

    public sealed record Dispatched(Guid RunId) : RunCreation;

    /// <summary>Created and waiting (BR-002); nothing enqueued.</summary>
    public sealed record QueuedAtCap(Guid RunId) : RunCreation;

    /// <summary>BR-001: the Story already has an active Run; nothing was written.</summary>
    public sealed record AlreadyActive : RunCreation;

    /// <summary>
    /// BR-007: the Story carries the hold, so nothing starts until a person clears it (DEC-067).
    /// Nothing was written. Like <see cref="AlreadyActive"/> this is an ordinary outcome rather
    /// than a fault — a held Story is a flow waiting on somebody, which is what it is for.
    /// </summary>
    public sealed record Held : RunCreation;

    /// <summary>The Project is retired: no new work, and the caller says so in its own voice.</summary>
    public sealed record ProjectArchived : RunCreation;

    /// <summary>The Run exists but the enqueue failed — visible as Queued with no DispatchedAt.</summary>
    public sealed record DispatchFailed(Guid RunId) : RunCreation;

    /// <summary>
    /// #210 — a locus precondition failed before any write (BR-016, impossible pairings). The
    /// sentence is the answer: Run now repeats it to the human; matching logs it and moves on.
    /// </summary>
    public sealed record PreconditionFailed(string Reason) : RunCreation;
}
