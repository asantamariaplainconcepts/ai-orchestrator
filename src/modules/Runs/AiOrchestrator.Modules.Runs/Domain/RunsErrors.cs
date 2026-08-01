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

    /// <summary>
    /// #121. The rule belongs to Projects, but the sentence belongs here: a Contracts assembly
    /// carries interfaces and data, never behaviour, and the architecture test says so.
    /// </summary>
    public static Error ProjectArchived(Guid projectId) =>
        Error.Conflict(
            "Runs.ProjectArchived",
            $"Project '{projectId}' is archived and starts no new work. Restore it to run anything."
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

    /// <summary>
    /// Dismissal is a decision about a failure (#145); anything else is a caller who has
    /// misunderstood, and hearing so beats a silent no-op.
    /// </summary>
    public static Error NotAFailure(Guid runId, string state) =>
        Error.Validation(
            "Run.NotAFailure",
            $"Run '{runId}' is {state}, not Failed. Only a failure can be dismissed."
        );

    /// <summary>The Vendor lesson (#210): misspelled must not silently mean the default.</summary>
    public static Error UnknownLocus(string locus) =>
        Error.Validation("Runs.UnknownLocus", $"Locus must be 'Pod' or 'Local', not '{locus}'.");

    /// <summary>
    /// #210 — a locus precondition refused the dispatch before any write: BR-016's dirty tree,
    /// or a physically impossible pairing. The sentence was written where the check ran.
    /// </summary>
    public static Error LocusRefused(string reason) =>
        Error.Validation("Runs.LocusRefused", reason);
}
