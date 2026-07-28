using ErrorOr;

namespace AiOrchestrator.Modules.Projects.Domain;

/// <summary>
/// The module's domain errors. Every failure path names one of these — call sites never
/// construct ad-hoc errors, so the API's problem codes stay a closed set.
/// </summary>
static class ProjectErrors
{
    public static Error NameAlreadyTaken(string name) =>
        Error.Conflict("Project.NameAlreadyTaken", $"A project named '{name}' already exists.");

    public static Error NotFound(Guid id) =>
        Error.NotFound("Project.NotFound", $"Project '{id}' was not found.");

    /// <summary>The deliberate-act guard (#121, design D4), naming what to type.</summary>
    public static Error ArchiveNotConfirmed(string name) =>
        Error.Validation(
            "Project.ArchiveNotConfirmed",
            $"Type the project's name — '{name}' — to archive it. Archiving stops its polling, "
                + "its automations and any new run; everything it already did stays readable."
        );

    /// <summary>
    /// BR-003. Names the Automation it collides with: "invalid" leaves an Admin guessing which of
    /// their rules is in the way, and the whole point of a config-time gate is that the fix is
    /// obvious while they are still looking at the form.
    /// </summary>
    public static Error AutomationNotFound(Guid automationId) =>
        Error.NotFound(
            "Automation.NotFound",
            $"Automation '{automationId}' does not exist in this project."
        );

    /// <summary>
    /// The message teaches the rule rather than only enforcing it: it says how many Runs hold
    /// the Automation, and names disabling as the thing the Admin actually wants (BR-014).
    /// </summary>
    public static Error AutomationInUse(string label, int runs) =>
        Error.Conflict(
            "Automation.InUse",
            $"The automation on '{label}' cannot be deleted: {runs} "
                + $"{(runs == 1 ? "run references" : "runs reference")} it, and runs keep their "
                + "automation for the audit trail. Disable it instead — it will stop triggering "
                + "and its history stays intact."
        );

    public static Error TriggerOverlaps(string label, string? state, string conflictingTrigger) =>
        Error.Conflict(
            "Automation.TriggerOverlaps",
            $"A trigger on '{label}'{(state is null ? " (any state)" : $" in state '{state}'")} would "
                + $"match the same Stories as the existing enabled trigger {conflictingTrigger}. "
                + "Disable or narrow that one first."
        );
}
