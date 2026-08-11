using AiOrchestrator.Modules.Projects.Domain;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.UnitTests;

public class Project_Should_Constraint
{
    [Fact]
    public void Create_Should_AssignVersion7Identifier()
    {
        var project = Project.Create("Phoenix");

        project.Id.ShouldNotBe(Guid.Empty);
        project.Id.Version.ShouldBe(7);
    }

    [Fact]
    public void Create_Should_AssignTimeOrderedIdentifier()
    {
        var earlier = Project.Create("Earlier");
        var later = Project.Create("Later");

        // v7 stores a 48-bit big-endian timestamp in the leading bytes, which is what makes these
        // ids index well. Compare those bytes directly: Guid.CompareTo orders field-by-field and
        // does not reflect creation order. Two ids minted in the same millisecond tie, hence <= 0.
        var earlierTimestamp = earlier.Id.ToByteArray(bigEndian: true).AsSpan(0, 6);
        var laterTimestamp = later.Id.ToByteArray(bigEndian: true).AsSpan(0, 6);

        earlierTimestamp.SequenceCompareTo(laterTimestamp).ShouldBeLessThanOrEqualTo(0);
    }

    [Fact]
    public void Create_Should_KeepTheGivenName()
    {
        Project.Create("Phoenix").Name.ShouldBe("Phoenix");
    }

    [Fact]
    public void ANewProject_Should_HaveNoLifecycleStages()
    {
        // A stage exists only as a consequence of a claim (#310) — seeding a default lifecycle is
        // explicitly out of scope, so a brand-new project starts with nothing rather than with a
        // guess about how this team works.
        Project.Create("Phoenix").LifecycleStages.ShouldBeEmpty();
    }

    [Fact]
    public void Stages_Should_ReadBackInTheOrderTheyWereAdded()
    {
        // Array position is the order (design D1). Asserted directly, because every claim below
        // depends on "the order" meaning this and nothing else.
        Lifecycle("s1", "s2", "s3").LifecycleStages.ShouldBe(["s1", "s2", "s3"]);
    }

    [Fact]
    public void AStage_Should_InsertBeforeTheFirstOne()
    {
        // AC 4's whole point: a step can be placed *first*. In the derived graph this replaces there
        // was no "before the first step" at all, because the first step's trigger was the entry
        // point.
        var project = Lifecycle("s1", "s2", "s3");

        project.InsertStageBefore("s0", "s1").ShouldBeTrue();

        project.LifecycleStages.ShouldBe(["s0", "s1", "s2", "s3"]);
    }

    [Fact]
    public void AStage_Should_InsertBeforeAMiddleOneWithoutDisturbingTheOrder()
    {
        var project = Lifecycle("s1", "s2", "s3");

        project.InsertStageBefore("s1a", "s2").ShouldBeTrue();

        // Every pre-existing stage keeps its neighbours: AC 4 says the order of the existing stages
        // is unchanged, which is a statement about all of them, not only about the ones nearby.
        project.LifecycleStages.ShouldBe(["s1", "s1a", "s2", "s3"]);
    }

    [Fact]
    public void AStage_Should_NotInsertBeforeSomethingThatIsNotAStage()
    {
        var project = Lifecycle("s1", "s2");

        project.InsertStageBefore("s0", "nowhere").ShouldBeFalse();

        project.LifecycleStages.ShouldBe(["s1", "s2"]);
    }

    [Fact]
    public void AStage_Should_ResolveToTheStoredSpellingWhateverTheCase()
    {
        // DEC-056: the vendor treats Ai:Propose and ai:propose as one label, so one stage must not
        // appear twice in two spellings. Resolve returns the *stored* spelling, which is what stops
        // a claim from creating the second one.
        var project = Lifecycle("ai:grill", "ai:propose");

        project.ResolveStage("AI:PROPOSE").ShouldBe("ai:propose");
        project.ResolveStage("Ai:Grill").ShouldBe("ai:grill");
        project.ResolveStage("ai:sync").ShouldBeNull();
    }

    [Fact]
    public void AStage_Should_NotBeAddedTwiceInADifferentCase()
    {
        var project = Lifecycle("ai:propose");

        project.AppendStage("AI:Propose").ShouldBeFalse();
        project.InsertStageBefore("AI:PROPOSE", "ai:propose").ShouldBeFalse();

        project.LifecycleStages.ShouldBe(["ai:propose"]);
    }

    [Fact]
    public void AClaim_Should_LeaveAnExistingBoundaryAlone()
    {
        // AC 5's second half: moving a claim to a boundary that is already there changes the claim
        // and not the list, so no other Automation's transition can be disturbed by it.
        var project = Lifecycle("s1", "s2", "s3");

        project.ClaimTransition("s2", "s3").IsError.ShouldBeFalse();

        project.LifecycleStages.ShouldBe(["s1", "s2", "s3"]);
    }

    [Fact]
    public void AClaim_Should_CreateItsFromStageImmediatelyBeforeTheToStage()
    {
        // AC 4. This is the question the derived graph could not answer at all: in it, the first
        // step's trigger *was* the entry point, so there was no "before" to place anything into.
        var project = Lifecycle("s1", "s2");

        project.ClaimTransition("s0", "s1").IsError.ShouldBeFalse();

        project.LifecycleStages.ShouldBe(["s0", "s1", "s2"]);
    }

    [Fact]
    public void AClaim_Should_ExtendTheFlowPastItsLastStage()
    {
        var project = Lifecycle("s1", "s2");

        project.ClaimTransition("s2", "s3").IsError.ShouldBeFalse();

        project.LifecycleStages.ShouldBe(["s1", "s2", "s3"]);
    }

    [Fact]
    public void AClaim_Should_GiveAStagelessProjectItsFirstTwoStages()
    {
        // How a new project acquires a lifecycle without "seed a default lifecycle" coming into
        // scope (design D10): installing a starter tier is a sequence of claims, and the first one
        // has nothing to attach to.
        var project = Project.Create("Phoenix");

        project.ClaimTransition("ai:grill", "ai:propose").IsError.ShouldBeFalse();

        project.LifecycleStages.ShouldBe(["ai:grill", "ai:propose"]);
    }

    [Fact]
    public void SuccessiveClaims_Should_BuildTheOrderTheyWereClaimedIn()
    {
        var project = Project.Create("Phoenix");

        project.ClaimTransition("ai:grill", "ai:propose");
        project.ClaimTransition("ai:propose", "ai:implement");
        project.ClaimTransition("ai:implement", "ai:sync");

        project.LifecycleStages.ShouldBe(["ai:grill", "ai:propose", "ai:implement", "ai:sync"]);
    }

    [Fact]
    public void AClaim_Should_BeRefusedWhenItsStagesAreNotAdjacent()
    {
        // The disagreement storing both an order and each claim makes possible: s1 → s3 while the
        // list says s1, s2, s3. Refused at the write, and the lifecycle is left exactly as it was.
        var project = Lifecycle("s1", "s2", "s3");

        var refusal = project.ClaimTransition("s1", "s3");

        refusal.IsError.ShouldBeTrue();
        refusal.FirstError.Code.ShouldBe("Automation.StagesNotAdjacent");
        // The refusal names the flow it is refusing against, so the fix is visible without a second
        // request — the reason BR-003's refusal names the conflicting Automation.
        refusal.FirstError.Description.ShouldContain("s1 → s2 → s3");
        project.LifecycleStages.ShouldBe(["s1", "s2", "s3"]);
    }

    [Fact]
    public void AClaim_Should_BeRefusedBackwardsAlongTheFlow()
    {
        var project = Lifecycle("s1", "s2", "s3");

        project.ClaimTransition("s3", "s2").IsError.ShouldBeTrue();
        project.LifecycleStages.ShouldBe(["s1", "s2", "s3"]);
    }

    [Fact]
    public void AClaim_Should_UseTheStageThatExistsWhateverTheCase()
    {
        // DEC-056 carried into the claim itself. A case-sensitive read here would create a second
        // spelling of one stage, which is exactly the edge the canvas silently drops today.
        var project = Lifecycle("ai:grill", "ai:propose");

        project.ClaimTransition("AI:Grill", "AI:PROPOSE").IsError.ShouldBeFalse();

        project.LifecycleStages.ShouldBe(["ai:grill", "ai:propose"]);
    }

    [Fact]
    public void AnAutomationClaimingNothing_Should_ContributeNoStage()
    {
        // DEC-053's standalone Automation, and the last stage of any lifecycle (design D3). Null is
        // an answer here, so a claim of nothing is a no-op rather than a refusal.
        var project = Lifecycle("s1", "s2");

        project.ClaimTransition("s1", null).IsError.ShouldBeFalse();
        project.ClaimTransition("ai:estimate", "").IsError.ShouldBeFalse();

        project.LifecycleStages.ShouldBe(["s1", "s2"]);
    }

    /// <summary>A project whose lifecycle holds these stages, in this order.</summary>
    static Project Lifecycle(params string[] stages)
    {
        var project = Project.Create("Phoenix");
        foreach (var stage in stages)
        {
            project.AppendStage(stage);
        }

        return project;
    }
}
