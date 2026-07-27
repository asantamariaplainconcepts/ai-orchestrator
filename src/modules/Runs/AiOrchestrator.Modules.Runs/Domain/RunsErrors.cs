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

    /// <summary>Approve/reject only mean something while a Run is waiting for a decision.</summary>
    public static Error RunNotAwaitingApproval(Guid runId, string state) =>
        Error.Conflict(
            "Runs.NotAwaitingApproval",
            $"Run '{runId}' is {state}, not awaiting approval — there is no Plan to decide on."
        );

    /// <summary>Cancelling something already finished is a question with no answer (design D4).</summary>
    public static Error RunAlreadyFinished(Guid runId, string state) =>
        Error.Conflict(
            "Runs.AlreadyFinished",
            $"Run '{runId}' is {state} — a finished Run cannot be cancelled."
        );

    public static Error RunNotFound(Guid runId) =>
        Error.NotFound("Runs.NotFound", $"Run '{runId}' does not exist.");
}
