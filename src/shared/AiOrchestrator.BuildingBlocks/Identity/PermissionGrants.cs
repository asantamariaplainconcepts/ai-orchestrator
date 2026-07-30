using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.BuildingBlocks.Identity;

/// <summary>
/// Which bundle holds which permission (BR-009, DEC-034). The indirection is the point: operations
/// name permissions, roles are bundles <i>over</i> permissions, and the mapping lives here — so
/// DEC-034's "custom roles post-MVP" becomes a change to this table rather than a sweep over every
/// declaration in the product.
/// <para>
/// Contributed by each module during its own <c>Add()</c>, so no module needs to know another's
/// permissions and nothing central has to be edited when a slice adds one. The shape is
/// ds-connect's, which reached it first and for the same reason.
/// </para>
/// </summary>
public sealed class PermissionGrants
{
    readonly Dictionary<ProjectRole, HashSet<string>> _byRole = [];

    public void Grant(ProjectRole role, string permission)
    {
        if (!_byRole.TryGetValue(role, out var permissions))
        {
            permissions = new HashSet<string>(StringComparer.Ordinal);
            _byRole[role] = permissions;
        }

        permissions.Add(permission);
    }

    /// <summary>
    /// Whether this bundle holds this permission on the project it was read for.
    /// <para>
    /// <b>Admin holds everything, by rule rather than by list.</b> DEC-034 says "Admin = all", and
    /// enumerating it instead would be one line per permission that can silently miss the next one —
    /// a permission nobody remembered to grant Admin would be refused to the only bundle that is
    /// defined as holding it. The table therefore carries the bundles that are a subset, which today
    /// means Member, and would mean any custom role.
    /// </para>
    /// </summary>
    public bool Holds(ProjectRole role, string permission) =>
        role == ProjectRole.Admin
        || (_byRole.TryGetValue(role, out var permissions) && permissions.Contains(permission));

    /// <summary>What this bundle has been granted explicitly — for the tests that police the table.</summary>
    public IReadOnlySet<string> GrantedTo(ProjectRole role) =>
        _byRole.TryGetValue(role, out var permissions)
            ? permissions
            : new HashSet<string>(StringComparer.Ordinal);
}

public static class PermissionGrantExtensions
{
    /// <summary>
    /// Registers that <paramref name="role"/> holds every permission listed. Called from a module's
    /// <c>Add()</c>, beside the use cases that declare them, so the grant and the declaration are
    /// reviewed together.
    /// </summary>
    public static IServiceCollection AddPermissionGrants(
        this IServiceCollection services,
        ProjectRole role,
        params string[] permissions
    )
    {
        services.Configure<PermissionGrants>(grants =>
        {
            foreach (var permission in permissions)
            {
                grants.Grant(role, permission);
            }
        });

        return services;
    }
}
