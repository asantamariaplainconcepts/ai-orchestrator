using AiOrchestrator.BuildingBlocks.Domain;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.UnitTests;

/// <summary>
/// The hold's identity (BR-007, DEC-067). One rule, and it is the vendor's: labels compare
/// case-insensitively (DEC-056), the same way BR-003's overlap check and matching already compare
/// them.
/// <para>
/// Worth its own test because the failure it prevents is silent. A case-sensitive comparison would
/// let a Story labelled <c>HITL</c> in the vendor's own casing run straight past a hold somebody
/// believed they had applied — no error, no Run refused, and a workflow that reads correctly.
/// That exact bug is on record here: two of the six label walks ADR-0022 retired compared through a
/// plain <c>Map</c> while product identity did not.
/// </para>
/// </summary>
public class StoryHold_Should_Constraint
{
    [Theory]
    [InlineData("hitl")]
    [InlineData("HITL")]
    [InlineData("Hitl")]
    [InlineData("  hitl  ")]
    public void AnyCasingOrPadding_Should_BeTheSameHold(string label) =>
        StoryHold.Is(label).ShouldBeTrue();

    [Theory]
    [InlineData("hitl-review")]
    [InlineData("needs-hitl")]
    [InlineData("hit")]
    [InlineData("")]
    [InlineData(null)]
    public void ALabelThatMerelyResemblesIt_Should_NotHold(string? label) =>
        StoryHold.Is(label).ShouldBeFalse();

    [Fact]
    public void ASetContainingTheHold_Should_HoldWhateverElseIsThere() =>
        StoryHold.IsHeld(["ai:implement", "HITL", "needs-design"]).ShouldBeTrue();

    [Fact]
    public void ASetWithoutIt_Should_NotHold() =>
        StoryHold.IsHeld(["ai:implement", "needs-design"]).ShouldBeFalse();

    [Fact]
    public void NoLabelsAtAll_Should_NotHold()
    {
        // An absent set is the ordinary case — every Story that nobody has held.
        StoryHold.IsHeld(null).ShouldBeFalse();
        StoryHold.IsHeld([]).ShouldBeFalse();
    }
}
