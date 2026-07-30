namespace AiOrchestrator.Modules.Runs;

/// <summary>
/// What can be done with a Project's Runs (BR-009). ACT-002's list is unusually explicit here —
/// view runs, logs and cost; trigger <i>Run now</i>; approve plans; cancel runs — so these
/// permissions are named to match it one for one, and the Member grant reads as that sentence.
/// </summary>
static class RunPermissions
{
    /// <summary>See Runs, their logs, their file changes and their cost.</summary>
    public const string Read = "run.read";

    /// <summary>Start one now (UC-012, DEC-035) — available to both bundles by decision.</summary>
    public const string Trigger = "run.trigger";

    /// <summary>Approve or reject a plan at the gate (UC-011).</summary>
    public const string Approve = "run.approve";

    /// <summary>Stop one that is running (UC-019).</summary>
    public const string Cancel = "run.cancel";

    /// <summary>
    /// Take a failure out of the inbox (#145). Not in ACT-002's list, and granted to Member anyway:
    /// it changes no configuration and destroys no record — the Run stays exactly as readable as it
    /// was — and an inbox only the Admin bundle could clear would stop being the shared queue UC-026
    /// describes.
    /// </summary>
    public const string DismissFailure = "run.failure.dismiss";
}
