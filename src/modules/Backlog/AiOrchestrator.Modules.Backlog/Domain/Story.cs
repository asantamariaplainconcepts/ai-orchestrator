using AiOrchestrator.BuildingBlocks.Domain;

namespace AiOrchestrator.Modules.Backlog.Domain;

/// <summary>
/// A mirrored work item (BC-002). This is a <b>read model</b> — the vendor is the source of truth
/// (BR-008), and nothing here is ever edited by the application.
/// <para>
/// Identity is <see cref="VendorId"/>, never the title: a renamed Story is the same Story.
/// <see cref="State"/> carries the <i>vendor's</i> state value rather than a canonical one,
/// because a canonical vocabulary cannot be chosen from a single vendor — that mapping belongs to
/// closing OPN-003 (design D9).
/// </para>
/// </summary>
sealed class Story : Aggregate
{
    Story() { }

    Story(
        Guid projectId,
        string vendorId,
        string title,
        string state,
        IReadOnlyCollection<string> labels,
        string? body
    )
    {
        ProjectId = projectId;
        VendorId = vendorId;
        Title = title;
        State = state;
        Labels = [.. labels];
        Body = body;
    }

    public Guid ProjectId { get; private set; }

    /// <summary>The vendor's own identifier. Stable across renames; the mirror's real key.</summary>
    public string VendorId { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string State { get; private set; } = string.Empty;

    public List<string> Labels { get; private set; } = [];

    /// <summary>
    /// The issue description — where the actual requirement lives. Mirrored verbatim (BR-008);
    /// sanitising happens at render, never at rest, or the Mirror would silently differ from
    /// the issue it mirrors (design D2).
    /// </summary>
    public string? Body { get; private set; }

    /// <summary>When this Story was last seen in a vendor response.</summary>
    public DateTimeOffset LastSeenAt { get; private set; }

    public static Story Create(
        Guid projectId,
        string vendorId,
        string title,
        string state,
        IReadOnlyCollection<string> labels,
        string? body,
        DateTimeOffset seenAt
    )
    {
        var story = new Story(projectId, vendorId, title, state, labels, body);
        story.LastSeenAt = seenAt;
        return story;
    }

    /// <summary>Applies what the vendor currently says. Returns true when anything actually changed.</summary>
    public bool UpdateFrom(
        string title,
        string state,
        IReadOnlyCollection<string> labels,
        string? body,
        DateTimeOffset seenAt
    )
    {
        // The body is in the comparison on purpose: an edited requirement is exactly the change
        // an Agent would want to react to, so it must announce itself like any other.
        var changed =
            Title != title || State != state || Body != body || !Labels.SequenceEqual(labels);

        Title = title;
        State = state;
        Labels = [.. labels];
        Body = body;
        LastSeenAt = seenAt;

        return changed;
    }
}
