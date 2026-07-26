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

    /// <summary>
    /// BR-003. Names the Automation it collides with: "invalid" leaves an Admin guessing which of
    /// their rules is in the way, and the whole point of a config-time gate is that the fix is
    /// obvious while they are still looking at the form.
    /// </summary>
    public static Error TriggerOverlaps(string label, string? state, string conflictingTrigger) =>
        Error.Conflict(
            "Automation.TriggerOverlaps",
            $"A trigger on '{label}'{(state is null ? " (any state)" : $" in state '{state}'")} would "
                + $"match the same Stories as the existing enabled trigger {conflictingTrigger}. "
                + "Disable or narrow that one first."
        );
}
