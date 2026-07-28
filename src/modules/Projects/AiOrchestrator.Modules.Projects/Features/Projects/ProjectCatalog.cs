using AiOrchestrator.Modules.Projects.Contracts;
using AiOrchestrator.Modules.Projects.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Projects.Features.Projects;

/// <summary>The Contracts read surface, implemented by the owner. One query, no cache (D1).</summary>
sealed class ProjectCatalog(ProjectsDbContext database) : IProjectCatalog
{
    public Task<bool> AcceptsWork(Guid projectId, CancellationToken cancellationToken = default) =>
        database.Projects.AnyAsync(
            project => project.Id == projectId && project.ArchivedAt == null,
            cancellationToken
        );
}
