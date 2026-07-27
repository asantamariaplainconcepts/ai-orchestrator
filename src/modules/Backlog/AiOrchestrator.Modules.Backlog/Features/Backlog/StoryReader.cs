using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Backlog.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog;

/// <summary>
/// The Contracts read surface, implemented by the owner. A Story the reconciler has removed
/// reads as null — the mirror reflects the vendor, and a consumer holding a stale event learns
/// that here rather than acting on a payload.
/// </summary>
sealed class StoryReader(BacklogDbContext database) : IStoryReader
{
    public async Task<StorySnapshot?> Find(
        Guid projectId,
        string vendorStoryId,
        CancellationToken cancellationToken = default
    ) =>
        await database
            .Stories.Where(story => story.ProjectId == projectId && story.VendorId == vendorStoryId)
            .Select(story => new StorySnapshot(
                story.ProjectId,
                story.VendorId,
                story.Title,
                story.State,
                story.Labels,
                story.Body
            ))
            .FirstOrDefaultAsync(cancellationToken);
}
