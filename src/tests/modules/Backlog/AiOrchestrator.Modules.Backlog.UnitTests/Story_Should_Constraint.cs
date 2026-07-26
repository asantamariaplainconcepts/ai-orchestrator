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
        var story = Story.Create(Guid.NewGuid(), "42", "Add login", "open", ["bug"], Now);

        story.UpdateFrom("Add login", "open", ["bug"], Now.AddMinutes(1)).ShouldBeFalse();
    }

    [Fact]
    public void UpdateFrom_Should_ReportChangeWhenTitleDiffers()
    {
        var story = Story.Create(Guid.NewGuid(), "42", "Add login", "open", ["bug"], Now);

        story.UpdateFrom("Add sign-in", "open", ["bug"], Now).ShouldBeTrue();
        story.Title.ShouldBe("Add sign-in");
    }

    [Fact]
    public void UpdateFrom_Should_ReportChangeWhenLabelsDiffer()
    {
        var story = Story.Create(Guid.NewGuid(), "42", "Add login", "open", ["bug"], Now);

        story.UpdateFrom("Add login", "open", ["bug", "ui"], Now).ShouldBeTrue();
        story.Labels.ShouldBe(["bug", "ui"]);
    }

    [Fact]
    public void UpdateFrom_Should_KeepIdentityAcrossARename()
    {
        var story = Story.Create(Guid.NewGuid(), "42", "Old title", "open", [], Now);
        var id = story.Id;

        story.UpdateFrom("Completely different title", "open", [], Now);

        // A renamed Story is the same Story — this is what stops the mirror duplicating on rename.
        story.VendorId.ShouldBe("42");
        story.Id.ShouldBe(id);
    }

    [Fact]
    public void UpdateFrom_Should_AlwaysAdvanceLastSeen()
    {
        var story = Story.Create(Guid.NewGuid(), "42", "Add login", "open", [], Now);
        var later = Now.AddMinutes(5);

        story.UpdateFrom("Add login", "open", [], later);

        // Unchanged content still means "seen just now" — otherwise a stable Story looks stale.
        story.LastSeenAt.ShouldBe(later);
    }
}
