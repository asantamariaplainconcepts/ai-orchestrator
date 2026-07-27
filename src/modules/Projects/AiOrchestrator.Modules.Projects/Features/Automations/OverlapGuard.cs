using AiOrchestrator.Modules.Projects.Domain;
using AiOrchestrator.Modules.Projects.Persistence;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

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
        // Only this Project's Automations can conflict, and only on the same label — so the
        // comparison set is small enough to evaluate in memory, where the domain rule lives.
        // Pushing Overlaps() into SQL would split the rule across two languages.
        var candidates = await database
            .Automations.Where(existing =>
                existing.ProjectId == projectId
                && existing.TriggerLabel == candidate.TriggerLabel
                && (excluding == null || existing.Id != excluding)
            )
            .ToListAsync(cancellationToken);

        return candidates.Find(candidate.Overlaps) is { } conflict
            ? ProjectErrors.TriggerOverlaps(
                candidate.TriggerLabel,
                candidate.TriggerState,
                Describe(conflict)
            )
            : Result.Success;
    }

    static string Describe(Automation automation) =>
        automation.TriggerState is null
            ? $"'{automation.TriggerLabel}' (any state)"
            : $"'{automation.TriggerLabel}' in state '{automation.TriggerState}'";
}
