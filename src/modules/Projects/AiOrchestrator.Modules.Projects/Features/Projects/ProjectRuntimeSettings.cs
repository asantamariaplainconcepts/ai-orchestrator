using AiOrchestrator.Modules.Projects.Contracts;
using AiOrchestrator.Modules.Projects.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Projects.Features.Projects;

/// <summary>The Contracts read surface for runtime resolution, implemented by the owner.</summary>
sealed class ProjectRuntimeSettings(ProjectsDbContext database) : IProjectRuntimeSettings
{
    public async Task<ProjectRuntimeResolution> Resolve(
        Guid projectId,
        CancellationToken cancellationToken = default
    )
    {
        var project = await database
            .Projects.Include(entity => entity.RuntimeCredentials)
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == projectId, cancellationToken);

        return project is null
            ? ProjectRuntimeResolution.None
            : new ProjectRuntimeResolution(
                project.DefaultRuntime,
                project.RuntimeCredentials.ToDictionary(
                    credential => credential.Runtime,
                    credential => credential.SecretName,
                    StringComparer.Ordinal
                )
            );
    }
}
