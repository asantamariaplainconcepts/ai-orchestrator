using AiOrchestrator.BuildingBlocks.Identity;

namespace AiOrchestrator.Modules.Runs.Features.Observation;

/// <summary>
/// Whether this caller may reach the machine's own sandboxes (#311) — <c>run.attach</c> read at the
/// habitat's scope rather than a project's.
/// <para>
/// <b>Why a project-scoped check cannot answer this.</b> <see cref="IProjectPermissions.RoleOn"/> needs a
/// project named, and the sandboxes this surface exists to reach include the one an earlier process
/// abandoned — which resolves to no Run and therefore to no project. The alternatives were to drop those
/// sandboxes, which is #304 again under a new name, or to widen the scope. This is the widening, written
/// down where it happens.
/// </para>
/// <para>
/// <b>What keeps it small.</b> ADR-0021 confines the whole surface to self-host, and DEC-016 fixes
/// self-host as one owner and one machine — the same assumption that already lets the startup sweep
/// delete any sandbox in the claimed namespace without asking whose Run it was. A caller holding
/// <c>run.attach</c> anywhere on such a machine is that owner. The caller of this method must have
/// answered the habitat question first, so a deployment never arrives here at all.
/// </para>
/// </summary>
static class MachineTerminalAccess
{
    public static async Task<bool> MayAttachSomewhere(
        IProjectPermissions permissions,
        PermissionGrants grants,
        CancellationToken cancellationToken
    )
    {
        var visible = await permissions.VisibleProjects(cancellationToken);

        if (visible is null)
        {
            // <b>Seeing every project is this codebase's whole-machine trust signal, not a shortcut.</b>
            // Only two implementations answer null: `SoleOccupantPermissions`, the habitat with one
            // caller who owns the machine, and `StoredProjectRoles` for a configured bootstrap
            // administrator. Both pair it with Admin on every project, so there is no caller for whom
            // null visibility and an absent `run.attach` can coexist — and a machine with no projects
            // yet still has sandboxes, so a check that demanded a project would lock its own owner out.
            return true;
        }

        // A bounded set means roles as rows. Then the question is the ordinary one, asked per project:
        // holding `run.attach` anywhere on this machine is what the habitat's scope means.
        foreach (var project in visible)
        {
            var role = await permissions.RoleOn(project, cancellationToken);
            if (role is not null && grants.Holds(role.Value, RunPermissions.Attach))
            {
                return true;
            }
        }

        return false;
    }
}
