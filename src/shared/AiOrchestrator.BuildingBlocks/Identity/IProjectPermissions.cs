namespace AiOrchestrator.BuildingBlocks.Identity;

/// <summary>
/// What the caller may do, on a named project (#13, BR-009). The question the principal cannot
/// answer: roles are per project, so "their role" is only a fact once a project is named
/// (design D2).
/// <para>
/// Composed per habitat like every other seam here. Where one person owns the machine there is
/// nothing to look up; where people sign in, the answer is rows plus the configured bootstrap
/// administrators (design D4).
/// </para>
/// </summary>
public interface IProjectPermissions
{
    /// <summary>
    /// The caller's role on this project, or <c>null</c> when they hold none — which is also the
    /// answer for a project that does not exist, deliberately: the two must be indistinguishable
    /// or a refusal becomes a way to enumerate projects.
    /// </summary>
    Task<ProjectRole?> RoleOn(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// The projects this caller may see, or <c>null</c> meaning <b>all of them</b> — the owner, the
    /// self-host habitat, a bootstrap administrator. Null rather than an enumeration of every id
    /// so that "everything" costs no query, and so consumers filter with one expression:
    /// <c>if (visible is not null) query = query.Where(row => visible.Contains(row.ProjectId));</c>
    /// </summary>
    Task<IReadOnlySet<Guid>?> VisibleProjects(CancellationToken cancellationToken);
}

/// <summary>
/// BR-009's bundles, as DEC-034 fixed them: Admin is everything on that project, Member observes
/// and triggers. Scoped to one project — a tenant-wide administrator is deliberately not a
/// concept here (design D6).
/// </summary>
public enum ProjectRole
{
    Member = 1,
    Admin = 2,
}
