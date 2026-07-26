using ErrorOr;

namespace AiOrchestrator.Modules.Runs.Domain;

/// <summary>
/// The module's closed set of domain errors — the same discipline as the Backlog's: call sites
/// never construct errors ad hoc, so the API's problem codes stay finite.
/// </summary>
static class RunsErrors
{
    /// <summary>The mirror has no such Story — nothing to run against.</summary>
    public static Error StoryNotFound(string vendorStoryId) =>
        Error.NotFound(
            "Runs.StoryNotFound",
            $"No mirrored story '{vendorStoryId}' exists in this project."
        );

    /// <summary>Disabled, deleted, or another project's — one refusal, because the fix is the same: pick one the catalog offers.</summary>
    public static Error AutomationNotAvailable(Guid automationId) =>
        Error.Validation(
            "Runs.AutomationNotAvailable",
            $"Automation '{automationId}' is not enabled on this project."
        );

    /// <summary>BR-001 — answering the human where matching stays silent.</summary>
    public static Error StoryHasActiveRun(string vendorStoryId) =>
        Error.Conflict(
            "Runs.StoryHasActiveRun",
            $"Story '{vendorStoryId}' already has an active Run (BR-001: one active Run per Story). "
                + "Wait for it to finish, or cancel it once cancellation exists."
        );

    /// <summary>BR-007's two-phase lane — the stated limitation, not silence.</summary>
    public static Error TwoPhaseNotImplemented(Guid automationId) =>
        Error.Validation(
            "Runs.TwoPhaseNotImplemented",
            $"Automation '{automationId}' requires approval, and the two-phase lane is not "
                + "implemented yet. No Run was created."
        );
}
