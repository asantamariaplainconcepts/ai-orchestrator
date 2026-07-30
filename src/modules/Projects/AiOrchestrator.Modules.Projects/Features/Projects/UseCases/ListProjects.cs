using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Projects.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Projects.Features.Projects.UseCases;

/// <summary>
/// UC-007's project side — the query exemplar: <see cref="IQuery{T}"/> and
/// <see cref="IAppQueryHandler{TQuery, TResponse}"/> travelling the same pipeline as commands.
/// <para>
/// Archived Projects are excluded by default and counted rather than hidden (#121): a list that
/// silently drops rows teaches its reader that things vanish, which is the opposite of what
/// archiving promises.
/// </para>
/// </summary>
sealed class ListProjects : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects",
                async (
                    bool? includeArchived,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                    Results.Ok(
                        await sender.Send(new Query(includeArchived ?? false), cancellationToken)
                    )
            )
            .WithName(nameof(ListProjects))
            .WithTags("Projects");

    internal sealed record Item(Guid Id, string Name, DateTimeOffset? ArchivedAt);

    /// <summary><paramref name="ArchivedCount"/> is stated even when the rows are excluded.</summary>
    internal sealed record Response(IReadOnlyList<Item> Projects, int ArchivedCount);

    [Requires(Access.FiltersToCaller)]
    internal sealed record Query(bool IncludeArchived) : IQuery<Response>;

    internal sealed class Handler(ProjectsDbContext database, IProjectPermissions permissions)
        : IAppQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            // What FiltersToCaller commits to (#13, design D7). Without it a signed-in person
            // holding no role saw every Project's name while every operation on them was refused —
            // which contradicts the refusals themselves, since those are worded so as not to
            // disclose that a Project exists.
            var visible = await permissions.VisibleProjects(cancellationToken);

            var mine = database.Projects.AsQueryable();
            if (visible is not null)
            {
                mine = mine.Where(project => visible.Contains(project.Id));
            }

            var projects = await mine.Where(project =>
                    query.IncludeArchived || project.ArchivedAt == null
                )
                .OrderBy(project => project.Name)
                .Select(project => new Item(project.Id, project.Name, project.ArchivedAt))
                .ToListAsync(cancellationToken);

            // Counted over the same filtered set: a count of Projects they cannot see would be the
            // disclosure the filter just prevented, in one number.
            var archived = await mine.CountAsync(
                project => project.ArchivedAt != null,
                cancellationToken
            );

            return new Response(projects, archived);
        }
    }
}
