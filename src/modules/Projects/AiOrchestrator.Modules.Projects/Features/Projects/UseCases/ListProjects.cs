using AiOrchestrator.BuildingBlocks.CQS;
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

    internal sealed record Query(bool IncludeArchived) : IQuery<Response>;

    internal sealed class Handler(ProjectsDbContext database) : IAppQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            var projects = await database
                .Projects.Where(project => query.IncludeArchived || project.ArchivedAt == null)
                .OrderBy(project => project.Name)
                .Select(project => new Item(project.Id, project.Name, project.ArchivedAt))
                .ToListAsync(cancellationToken);

            var archived = await database.Projects.CountAsync(
                project => project.ArchivedAt != null,
                cancellationToken
            );

            return new Response(projects, archived);
        }
    }
}
