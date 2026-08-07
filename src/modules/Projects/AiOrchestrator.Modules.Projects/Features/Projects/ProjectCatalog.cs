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

    // Archived included on purpose: an entry for a Run that already exists still deserves its
    // Project's name — the question is "which project?", not "does it accept work?".
    public Task<string?> Name(Guid projectId, CancellationToken cancellationToken = default) =>
        database
            .Projects.Where(project => project.Id == projectId)
            .Select(project => (string?)project.Name)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> ActiveProjectIds(
        CancellationToken cancellationToken = default
    ) =>
        await database
            .Projects.Where(project => project.ArchivedAt == null)
            .Select(project => project.Id)
            .ToListAsync(cancellationToken);
}
