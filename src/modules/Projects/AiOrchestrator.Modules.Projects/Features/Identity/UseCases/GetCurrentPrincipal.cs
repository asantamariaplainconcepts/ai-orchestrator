using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Projects.Domain;
using AiOrchestrator.Modules.Projects.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Projects.Features.Identity.UseCases;

/// <summary>
/// Who the portal is talking to, and what they may do where (#119, reshaped by #13 design D5).
/// <para>
/// It used to return one role. After roles became per project, "your role" is not a fact — so
/// returning one would be inventing an answer. It reports the caller plus their role on each
/// project they can see, which is what a screen actually needs: the shell shows the name, and a
/// screen that cares about permission asks about the project it is on.
/// </para>
/// <para>
/// No query handler, deliberately, even now that it reads: the answer is this habitat's
/// composition plus the caller's own rows, and there is nothing here another use case would send.
/// </para>
/// </summary>
sealed class GetCurrentPrincipal : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/me",
                async (
                    ICurrentPrincipal principal,
                    IProjectPermissions permissions,
                    ProjectsDbContext database,
                    KnownPeople people,
                    CancellationToken cancellationToken
                ) =>
                {
                    var caller = principal.Current;

                    // The first request every screen makes, and the first a new arrival makes — so
                    // "has signed in at least once" becomes true at the moment it is true, with no
                    // sign-in hook to keep in step (task 4.1).
                    await people.Note(caller, cancellationToken);
                    try
                    {
                        await database.SaveChangesAsync(cancellationToken);
                    }
                    catch (DbUpdateException)
                    {
                        // Two tabs opening at once both read "not known" and both insert. The unique
                        // index settles it; losing that race means the row exists, which is the
                        // outcome wanted. Cleared rather than retried, because nothing below reads
                        // what we just wrote.
                        database.ChangeTracker.Clear();
                    }

                    var visible = await permissions.VisibleProjects(cancellationToken);

                    // Live projects only: an archived one is not somewhere anybody is working, and
                    // listing it here would put it in front of every screen that reads this.
                    var projects = database.Projects.Where(project => project.ArchivedAt == null);
                    if (visible is not null)
                    {
                        projects = projects.Where(project => visible.Contains(project.Id));
                    }

                    var named = await projects
                        .OrderBy(project => project.Name)
                        .Select(project => new { project.Id, project.Name })
                        .ToListAsync(cancellationToken);

                    // One RoleOn per project rather than a join: the seam is what decides, and it
                    // knows about the configured administrators that no row mentions. A join would
                    // have to reimplement that and would quietly disagree with the pipeline.
                    var standing = new List<ProjectStanding>(named.Count);
                    foreach (var project in named)
                    {
                        var role = await permissions.RoleOn(project.Id, cancellationToken);
                        if (role is null)
                        {
                            continue;
                        }

                        standing.Add(
                            new ProjectStanding(project.Id, project.Name, role.ToString()!)
                        );
                    }

                    return Results.Ok(new Response(caller.Id, caller.DisplayName, standing));
                }
            )
            .WithName(nameof(GetCurrentPrincipal))
            .WithTags("Identity");

    internal sealed record Response(
        string Id,
        string DisplayName,
        IReadOnlyList<ProjectStanding> Projects
    );

    internal sealed record ProjectStanding(Guid ProjectId, string Name, string Role);
}
