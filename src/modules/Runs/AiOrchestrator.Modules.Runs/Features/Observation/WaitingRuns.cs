using AiOrchestrator.Modules.Runs.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Features.Observation;

/// <summary>
/// The one predicate for "this Run waits on a human" (#145, design D5).
/// <para>
/// The inbox lists these and the pulse counts them, and until now each held its own copy of the
/// condition with a comment saying the two must never disagree. A comment is not a mechanism: adding
/// the dismissal to one copy and not the other is precisely how they would have come apart, and a
/// count that disagrees with its list shows a Member "1 waiting" above an empty page.
/// </para>
/// </summary>
static class WaitingRuns
{
    /// <summary>
    /// Applied to the whole set, because the newer-Run test has to see every Run for the Story —
    /// callers scope afterwards, which SQL is free to reorder.
    /// </summary>
    public static IQueryable<Run> WaitingOnAHuman(this DbSet<Run> runs) =>
        runs.Where(run =>
            run.State == RunState.AwaitingApproval
            || run.State == RunState.AwaitingInput
            || (
                run.State == RunState.Failed
                // Derived: a fact about the world (#94, design D2).
                && !runs.Any(newer =>
                    newer.ProjectId == run.ProjectId
                    && newer.VendorStoryId == run.VendorStoryId
                    && newer.CreatedAt > run.CreatedAt
                )
                // Stored: a fact about a person, which no query could derive (#145, design D2).
                && run.DismissedAt == null
            )
        );
}
