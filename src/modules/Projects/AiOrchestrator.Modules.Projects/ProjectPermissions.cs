namespace AiOrchestrator.Modules.Projects;

/// <summary>
/// What can be done to a Project and its Automations (BR-009). See
/// <c>BacklogPermissions</c> for why these are dotted strings rather than an enum.
/// </summary>
static class ProjectPermissions
{
    /// <summary>Retire the Project or bring it back (UC-025) — its most consequential act.</summary>
    public const string Archive = "project.archive";

    /// <summary>See who holds what here, and change it (UC-002).</summary>
    public const string ManageRoles = "project.roles.manage";

    /// <summary>See the Automations that will act on this Project's Stories.</summary>
    public const string ReadAutomations = "project.automations.read";

    /// <summary>
    /// Create, edit, enable, disable or delete an Automation. ACT-002 explicitly may **not**:
    /// an Automation decides when an agent touches a repository, and that is configuration.
    /// </summary>
    public const string ManageAutomations = "project.automations.manage";
}
