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
/// </summary>
sealed class ListProjects : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects",
                async (ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(ListProjects))
            .WithTags("Projects");

    internal sealed record Item(Guid Id, string Name);

    internal sealed record Query : IQuery<IReadOnlyList<Item>>;

    internal sealed class Handler(ProjectsDbContext database)
        : IAppQueryHandler<Query, IReadOnlyList<Item>>
    {
        public async Task<IReadOnlyList<Item>> Handle(
            Query query,
            CancellationToken cancellationToken
        ) =>
            await database
                .Projects.OrderBy(project => project.Name)
                .Select(project => new Item(project.Id, project.Name))
                .ToListAsync(cancellationToken);
    }
}
