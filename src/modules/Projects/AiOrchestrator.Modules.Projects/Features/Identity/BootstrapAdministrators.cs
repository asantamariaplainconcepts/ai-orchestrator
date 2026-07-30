using AiOrchestrator.BuildingBlocks.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AiOrchestrator.Modules.Projects.Features.Identity;

/// <summary>
/// The provider object ids that hold Admin on every Project (#13, design D4).
/// <para>
/// Project-scoped rows create a chicken and egg: the first person to sign in holds no role, so
/// nobody can grant one. The rejected answer was first-signed-in-user-claims-Admin — that grants
/// power by race, whoever reaches the URL first, which is #12's interim rule with extra steps and
/// a worse story because now it is permanent and invisible.
/// </para>
/// <para>
/// Configuration instead: race-free, auditable, revocable without a deploy, and it reuses the
/// presence-of-configuration idiom every habitat decision here already follows. The ids are
/// deployed as a repository variable — object ids are not secrets, and they stay out of git
/// regardless.
/// </para>
/// </summary>
sealed record BootstrapAdministrators(IReadOnlySet<string> IdentityIds)
{
    public const string ConfigurationKey = "Auth:BootstrapAdmins";

    public bool Contains(string identityId) => IdentityIds.Contains(identityId);

    /// <summary>
    /// Reads both shapes on purpose. A JSON array is what appsettings wants; a separated string is
    /// what an environment variable — and therefore a repository variable, and therefore Terraform
    /// — can actually carry. Supporting only the first would mean the deployed habitat could never
    /// name an administrator.
    /// </summary>
    public static BootstrapAdministrators From(IConfiguration configuration)
    {
        var section = configuration.GetSection(ConfigurationKey);

        var listed = section
            .GetChildren()
            .Select(child => child.Value)
            .Concat(
                (section.Value ?? string.Empty).Split(
                    [',', ';'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
            )
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new BootstrapAdministrators(listed);
    }
}

/// <summary>
/// Roles as rows, for the habitat where people sign in (#13, design D3). Registered only there:
/// which habitat this is comes from <see cref="IdentityHabitat"/>, never from inspecting the
/// caller. An earlier draft asked the principal whether it was a "sole occupant" and derived that
/// from its id — and since the provider mode calls its pre-sign-in caller
/// <see cref="Principal.AnonymousId"/>, exactly as the provider-less habitat calls its only caller,
/// that would have read "nobody is signed in" as "this person owns the machine".
/// </summary>
sealed class StoredProjectRoles(
    Persistence.ProjectsDbContext database,
    ICurrentPrincipal principal,
    BootstrapAdministrators administrators
) : IProjectPermissions
{
    public async Task<ProjectRole?> RoleOn(Guid projectId, CancellationToken cancellationToken)
    {
        var caller = principal.Current;

        if (administrators.Contains(caller.Id))
        {
            return ProjectRole.Admin;
        }

        // Nullable cast so "no row" stays distinguishable from a stored role: without it
        // FirstOrDefaultAsync yields the enum's default for somebody who holds nothing at all.
        return await database
            .ProjectRoles.Where(row => row.ProjectId == projectId && row.IdentityId == caller.Id)
            .Select(row => (ProjectRole?)row.Role)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>?> VisibleProjects(CancellationToken cancellationToken)
    {
        var caller = principal.Current;

        if (administrators.Contains(caller.Id))
        {
            return null;
        }

        var ids = await database
            .ProjectRoles.Where(row => row.IdentityId == caller.Id)
            .Select(row => row.ProjectId)
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }
}

/// <summary>
/// The habitats with one caller: a machine its owner owns, and DEC-049's self-host deployment with
/// no provider — which already announces at startup that it authenticates nobody.
/// <para>
/// There is nothing to look up, and deliberately no table: what this knows is that there is one
/// person, which is the same reason <c>LocalOwner</c> is built from configuration and never stored.
/// It exists so those habitats keep working unchanged after roles arrive — the alternative was a
/// role table somebody has to seed before their own machine will let them configure anything.
/// </para>
/// </summary>
sealed class SoleOccupantPermissions : IProjectPermissions
{
    public Task<ProjectRole?> RoleOn(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult<ProjectRole?>(ProjectRole.Admin);

    public Task<IReadOnlySet<Guid>?> VisibleProjects(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>?>(null);
}
