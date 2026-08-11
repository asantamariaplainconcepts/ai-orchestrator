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

    /// <summary>
    /// Design D6's limitation with a voice (#13). A role attaches to a provider identity, and that
    /// identity does not exist here until they sign in once — so this is a refusal rather than a
    /// stored row keyed on a name, which would follow whoever inherits the mailbox (design D3).
    /// </summary>
    public static Error PersonUnknown() =>
        Error.Validation(
            "ProjectRole.PersonUnknown",
            "That person has not signed in to this deployment yet, so there is no identity to give "
                + "a role to. Ask them to sign in once; they will appear in the list afterwards."
        );

    /// <summary>
    /// The unrecoverable state this refuses to create: a Project whose last administrator removed
    /// or demoted themselves, which nobody can undo from inside the product.
    /// </summary>
    public static Error LastAdministrator() =>
        Error.Conflict(
            "ProjectRole.LastAdministrator",
            "This is the project's only administrator, and a project with none cannot be "
                + "configured by anyone again. Give somebody else the Admin role first."
        );

    public static Error RoleNotGranted() =>
        Error.NotFound(
            "ProjectRole.NotGranted",
            "That person holds no role on this project, so there is nothing to remove."
        );

    /// <summary>
    /// The adjacency invariant's refusal (#310, design D4). An Automation claims <i>one step</i> of
    /// the flow, so a claim spanning two stages with something between them would make the stored
    /// order and the claim disagree — the failure DEC-053 avoided by not storing an order at all.
    /// <para>
    /// The message names the lifecycle it is refusing against, for the reason BR-003's does: a
    /// refusal that only says "invalid" leaves an Admin guessing which of their stages is in the
    /// way, while the whole point of a write-time gate is that the fix is obvious while they are
    /// still looking at it.
    /// </para>
    /// </summary>
    public static Error StagesNotAdjacent(
        string fromStage,
        string toStage,
        IReadOnlyList<string> lifecycle
    ) =>
        Error.Validation(
            "Automation.StagesNotAdjacent",
            $"'{fromStage}' and '{toStage}' are not next to each other in this project's flow "
                + $"({string.Join(" → ", lifecycle)}), so no single step goes from one to the other. "
                + "An Automation claims one step: its to-stage is the stage that follows its trigger "
                + "label. Claim an adjacent pair, or move the steps between them first."
        );

    public static Error TriggerOverlaps(string label, string? state, string conflictingTrigger) =>
        Error.Conflict(
            "Automation.TriggerOverlaps",
            $"A trigger on '{label}'{(state is null ? " (any state)" : $" in state '{state}'")} would "
                + $"match the same Stories as the existing enabled trigger {conflictingTrigger}. "
                + "Disable or narrow that one first."
        );
}
