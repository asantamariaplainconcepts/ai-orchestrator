using AiOrchestrator.BuildingBlocks.Dispatch;
using AiOrchestrator.Modules.Projects.Contracts;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AiOrchestrator.Modules.Runs.Features.Matching;

/// <summary>
/// The one Run-creation path (BR-013's structural consequence, design D1 of run-now): event
/// matching and Run now both come through here, so BR-001, BR-002 and the BR-007 lane split
/// are enforced by exactly one piece of code. The outcome is data; each caller chooses its
/// voice — the event handler stays silent where at-least-once makes silence correct, the
/// endpoint answers the human.
/// </summary>
sealed class RunCreator(
    RunsDbContext database,
    IRunDispatcher dispatcher,
    RunsOptions options,
    TimeProvider clock,
    ILogger<RunCreator> logger
)
{
    public async Task<RunCreation> Create(
        Guid projectId,
        string vendorStoryId,
        AutomationTrigger automation,
        CancellationToken cancellationToken
    )
    {
        if (automation.RequiresApproval)
        {
            // The stated limitation (module-integration-events → run-orchestration D6): the
            // two-phase lane is its own issue, and parking a Run now would freeze approval
            // semantics before it is designed.
            MatchingLog.TwoPhaseRefused(logger, automation.AutomationId);
            return new RunCreation.TwoPhaseRefused(automation.AutomationId);
        }

        // BR-001 pre-check keeps the common case quiet; the index owns the race below.
        var hasActiveRun = await database.Runs.AnyAsync(
            run => run.ProjectId == projectId && run.VendorStoryId == vendorStoryId,
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

        var run = Run.Create(projectId, vendorStoryId, automation.AutomationId, clock.GetUtcNow());
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

    /// <summary>BR-007's two-phase lane, not implemented yet; nothing was written.</summary>
    public sealed record TwoPhaseRefused(Guid AutomationId) : RunCreation;

    /// <summary>The Run exists but the enqueue failed — visible as Queued with no DispatchedAt.</summary>
    public sealed record DispatchFailed(Guid RunId) : RunCreation;
}
