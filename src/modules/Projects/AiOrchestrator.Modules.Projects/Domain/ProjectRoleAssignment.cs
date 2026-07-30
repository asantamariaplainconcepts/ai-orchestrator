using AiOrchestrator.BuildingBlocks.Domain;
using AiOrchestrator.BuildingBlocks.Identity;

namespace AiOrchestrator.Modules.Projects.Domain;

/// <summary>
/// One person's standing on one Project (#13, BR-009). The row is what makes DEC-034's bundles
/// real: without it, "Admin" was a value the host handed to everybody who signed in.
/// <para>
/// Keyed by the provider's object id, never an email (design D3). Emails change and get
/// reassigned, and a role that follows a mailbox follows whoever inherits it. The object id is
/// already what <see cref="Principal.Id"/> carries, so the two agree by construction rather than
/// by a mapping somebody has to maintain.
/// </para>
/// </summary>
sealed class ProjectRoleAssignment : Aggregate
{
    ProjectRoleAssignment() { }

    ProjectRoleAssignment(Guid projectId, string identityId, ProjectRole role, DateTimeOffset at)
    {
        ProjectId = projectId;
        IdentityId = identityId;
        Role = role;
        GrantedAt = at;
    }

    public Guid ProjectId { get; private set; }

    public string IdentityId { get; private set; } = string.Empty;

    public ProjectRole Role { get; private set; }

    public DateTimeOffset GrantedAt { get; private set; }

    public static ProjectRoleAssignment Grant(
        Guid projectId,
        string identityId,
        ProjectRole role,
        DateTimeOffset at
    ) => new(projectId, identityId, role, at);

    /// <summary>
    /// Changing a role keeps the row and its original moment: the grant is when this person was
    /// given standing on the Project, and promoting them is not a new relationship.
    /// </summary>
    public void ChangeTo(ProjectRole role) => Role = role;
}
