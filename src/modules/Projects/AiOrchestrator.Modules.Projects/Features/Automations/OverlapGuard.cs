using AiOrchestrator.Modules.Projects.Domain;
using AiOrchestrator.Modules.Projects.Persistence;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AiOrchestrator.Modules.Projects.Features.Automations;

/// <summary>
/// BR-003's gate, in one place for its three callers — create, edit and enable (design D1 of
/// automation-editing). #14's finding was that the interesting case is *subsumption*, which a
/// second implementation of this rule would be exactly the kind to miss; so there is one.
/// </summary>
sealed class OverlapGuard(ProjectsDbContext database)
{
    /// <summary>
    /// Refuses when an enabled sibling's trigger could match a Story this one would.
    /// <paramref name="excluding"/> is the subject's own id on an edit — an Automation must not
    /// be refused for colliding with the version of itself it is replacing.
    /// </summary>
    public async Task<ErrorOr<Success>> Check(
        Automation candidate,
        Guid projectId,
        Guid? excluding,
        CancellationToken cancellationToken
    )
    {
        // Only this Project's Automations can conflict, so the comparison set is small enough to
        // evaluate in memory, where the domain rule lives. Pushing the rule into SQL would split it
        // across two languages.
        //
        // The label filter that used to be in this query has gone (#147): it compared with `==`,
        // which Postgres evaluates case-sensitively, so a differently-cased sibling was never even
        // fetched and the rule could not see the conflict it exists to catch.
        var candidates = await database
            .Automations.Where(existing =>
                existing.ProjectId == projectId && (excluding == null || existing.Id != excluding)
            )
            .ToListAsync(cancellationToken);

        // Subsumption among enabled siblings, and exact duplication regardless — two questions, so
        // two rules rather than one weakened (design D3).
        var conflict =
            candidates.Find(candidate.Overlaps) ?? candidates.Find(candidate.IsSameTriggerAs);

        return conflict is not null
            ? ProjectErrors.TriggerOverlaps(
                candidate.TriggerLabel,
                candidate.TriggerState,
                Describe(conflict)
            )
            : Result.Success;
    }

    /// <summary>
    /// The write lost the race (#147, design D2). Narrow on purpose, the same shape
    /// <c>RunCreator</c> and the Backlog reconciler use: only a unique-key violation means somebody
    /// else already did this.
    /// <para>
    /// Mapping it to BR-003's own refusal rather than letting it surface as an internal error is the
    /// point — the caller asked a question the rule answers, and the answer arriving from the index
    /// instead of from the guard does not change what it is. The guard stays because it can name the
    /// conflicting Automation, which a constraint violation cannot.
    /// </para>
    /// </summary>
    public static bool IsDuplicateTrigger(DbUpdateException exception) =>
        exception.InnerException
            is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    /// <summary>The refusal for a conflict discovered by the index, which knows no names.</summary>
    public static Error RaceLost(Automation candidate) =>
        ProjectErrors.TriggerOverlaps(
            candidate.TriggerLabel,
            candidate.TriggerState,
            "an Automation saved at the same moment"
        );

    static string Describe(Automation automation) =>
        automation.TriggerState is null
            ? $"'{automation.TriggerLabel}' (any state)"
            : $"'{automation.TriggerLabel}' in state '{automation.TriggerState}'";
}
