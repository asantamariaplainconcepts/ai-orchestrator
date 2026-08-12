using AiOrchestrator.BuildingBlocks.Domain;
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

    public async Task<IReadOnlyList<string>> VendorStoryIds(
        Guid projectId,
        CancellationToken cancellationToken = default
    ) =>
        await database
            .Stories.Where(story => story.ProjectId == projectId)
            .Select(story => story.VendorId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// The hold's test runs in memory, on purpose (#335, design D3). <c>Labels</c> is a
    /// <c>text[]</c> column, so a translated <c>Contains</c> compares case-<i>sensitively</i> and
    /// would report a Story labelled <c>HITL</c> as unheld — precisely the failure DEC-056's fold
    /// exists to prevent. Expressing the fold as SQL would give
    /// <see cref="StoryHold"/> a second home, and two homes drift.
    /// <para>
    /// Three columns are projected rather than whole entities: the filter needs the labels, the
    /// caller needs id and title, and the body — a full requirement's text per Story — has no
    /// reader here.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<HeldStory>> Held(
        Guid projectId,
        CancellationToken cancellationToken = default
    )
    {
        var candidates = await database
            .Stories.Where(story => story.ProjectId == projectId)
            .Select(story => new
            {
                story.VendorId,
                story.Title,
                story.Labels,
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. candidates
                .Where(story => StoryHold.IsHeld(story.Labels))
                .Select(story => new HeldStory(story.VendorId, story.Title)),
        ];
    }
}
