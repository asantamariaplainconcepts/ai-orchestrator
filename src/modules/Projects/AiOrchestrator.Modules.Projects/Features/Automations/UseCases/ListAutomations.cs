using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Projects.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Projects.Features.Automations.UseCases;

/// <summary>UC-005's read side: the rules configured on a Project.</summary>
sealed class ListAutomations : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/automations",
                async (Guid projectId, ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new Query(projectId), cancellationToken))
            )
            .WithName(nameof(ListAutomations))
            .WithTags("Automations");

    [Requires(ProjectPermissions.ReadAutomations)]
    internal sealed record Query(Guid ProjectId)
        : IQuery<IReadOnlyList<CreateAutomation.Response>>,
            IScopedToProject;

    internal sealed class Handler(ProjectsDbContext database)
        : IAppQueryHandler<Query, IReadOnlyList<CreateAutomation.Response>>
    {
        public async Task<IReadOnlyList<CreateAutomation.Response>> Handle(
            Query query,
            CancellationToken cancellationToken
        )
        {
            // Materialise, then project in memory. The response carries enum *names*, and
            // ToString() inside an EF projection is translated to SQL rather than evaluated in
            // .NET — #7 shipped exactly that and the API returned "0" instead of "GitHub".
            var automations = await database
                .Automations.Where(automation => automation.ProjectId == query.ProjectId)
                .OrderBy(automation => automation.TriggerLabel)
                .ThenBy(automation => automation.TriggerState)
                .ToListAsync(cancellationToken);

            return [.. automations.Select(CreateAutomation.ToResponse)];
        }
    }
}
