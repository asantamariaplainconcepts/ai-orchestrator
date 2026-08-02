using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Backlog.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog;

/// <summary>
/// The one write behind <see cref="IPromptDirectoryWriter"/> (#229). It normalises through
/// <see cref="PromptPath"/> for the reason that class exists: the directory this stores is the
/// directory every later resolution composes with, so the two must agree about slashes.
/// </summary>
sealed class PromptDirectoryWriter(BacklogDbContext database) : IPromptDirectoryWriter
{
    public async Task<bool> UseDirectory(
        Guid projectId,
        string directory,
        CancellationToken cancellationToken = default
    )
    {
        var connector = await database.Connectors.FirstOrDefaultAsync(
            entity => entity.ProjectId == projectId,
            cancellationToken
        );

        if (connector is null)
        {
            return false;
        }

        connector.UsePromptDirectory(PromptPath.NormalizeDirectory(directory));
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }
}
