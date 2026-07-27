using AiOrchestrator.Modules.Backlog.Domain;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.UnitTests;

/// <summary>
/// The reconciliation contract lives on Story: identity is the vendor id, and an update reports
/// whether anything actually changed. Both matter — the first stops renames creating duplicates,
/// the second stops every poll looking like churn.
/// </summary>
public class Story_Should_Constraint
{
    static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UpdateFrom_Should_ReportNoChangeWhenNothingDiffers()
    {
        var story = Story.Create(
            Guid.NewGuid(),
            "42",
            "Add login",
            "open",
            ["bug"],
            "A description",
            Now
        );

        story
            .UpdateFrom("Add login", "open", ["bug"], "A description", Now.AddMinutes(1))
            .ShouldBeFalse();
    }

    [Fact]
    public void UpdateFrom_Should_ReportChangeWhenTitleDiffers()
    {
        var story = Story.Create(
            Guid.NewGuid(),
            "42",
            "Add login",
            "open",
            ["bug"],
            "A description",
            Now
        );

        story.UpdateFrom("Add sign-in", "open", ["bug"], "A description", Now).ShouldBeTrue();
        story.Title.ShouldBe("Add sign-in");
    }

    [Fact]
    public void UpdateFrom_Should_ReportChangeWhenLabelsDiffer()
    {
        var story = Story.Create(
            Guid.NewGuid(),
            "42",
            "Add login",
            "open",
            ["bug"],
            "A description",
            Now
        );

        story.UpdateFrom("Add login", "open", ["bug", "ui"], "A description", Now).ShouldBeTrue();
        story.Labels.ShouldBe(["bug", "ui"]);
    }

    [Fact]
    public void UpdateFrom_Should_ReportChangeWhenTheDescriptionDiffers()
    {
        var story = Story.Create(
            Guid.NewGuid(),
            "42",
            "Add login",
            "open",
            ["bug"],
            "Old text",
            Now
        );

        // An edited requirement is exactly the change an Agent would want to react to.
        story.UpdateFrom("Add login", "open", ["bug"], "New text", Now).ShouldBeTrue();
        story.Body.ShouldBe("New text");
    }

    [Fact]
    public void UpdateFrom_Should_KeepIdentityAcrossARename()
    {
        var story = Story.Create(
            Guid.NewGuid(),
            "42",
            "Old title",
            "open",
            [],
            "A description",
            Now
        );
        var id = story.Id;

        story.UpdateFrom("Completely different title", "open", [], "A description", Now);

        // A renamed Story is the same Story — this is what stops the mirror duplicating on rename.
        story.VendorId.ShouldBe("42");
        story.Id.ShouldBe(id);
    }

    [Fact]
    public void UpdateFrom_Should_AlwaysAdvanceLastSeen()
    {
        var story = Story.Create(
            Guid.NewGuid(),
            "42",
            "Add login",
            "open",
            [],
            "A description",
            Now
        );
        var later = Now.AddMinutes(5);

        story.UpdateFrom("Add login", "open", [], "A description", later);

        // Unchanged content still means "seen just now" — otherwise a stable Story looks stale.
        story.LastSeenAt.ShouldBe(later);
    }
}
