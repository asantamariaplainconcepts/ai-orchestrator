using AiOrchestrator.Modules.Backlog.Contracts;

namespace AiOrchestrator.Modules.Runs.Domain;

/// <summary>
/// How a Story is described to an agent — one way, whichever path supplies it (#189, design D3).
/// <para>
/// Before this existed the two paths disagreed: a Run supplied the number, state and labels with a
/// bounded description, and a conversation supplied title and body unbounded. That made trying a
/// prompt in a conversation and then running it from an Automation two different inputs, which is
/// exactly what a scratchpad must not be — and state and labels are the sort of thing a real prompt
/// branches on, so the difference was never cosmetic.
/// </para>
/// <para>
/// The Run's framing is the one kept, because it is the one that has to stay faithful. The
/// conversation adopted it and gained a bound it did not have.
/// </para>
/// </summary>
static class StoryDescription
{
    /// <summary>
    /// The requirement, bounded at the prompt rather than at rest (story-detail design D3): the
    /// Mirror keeps the vendor's whole body, but an unbounded prompt is a cost and timeout surprise.
    /// The truncation says so, because an Agent silently given half a requirement will confidently
    /// implement half a story.
    /// </summary>
    internal const int BodyLimit = 8000;

    public static string Of(StorySnapshot story) =>
        $"Story #{story.VendorStoryId}: {story.Title}\n"
        + $"State: {story.State}; labels: {string.Join(", ", story.Labels)}.\n\n"
        + $"Description:\n{Requirement(story.Body)}";

    static string Requirement(string? body) =>
        string.IsNullOrWhiteSpace(body) ? "(the story has no description)"
        : body.Length <= BodyLimit ? body
        : body[..BodyLimit] + "\n\n[description truncated by the orchestrator]";
}
