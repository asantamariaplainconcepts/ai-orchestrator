using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Domain;
using AiOrchestrator.Modules.Projects.Features.Automations;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.UnitTests;

/// <summary>
/// #190 — a starter prompt that fails to load is worse than none, because it is offered as working.
/// <para>
/// Every case here enumerates the catalogue rather than naming files, so adding a starter without
/// covering it is impossible rather than merely discouraged. The frontmatter assertions run the
/// product's own <see cref="PromptText.WithoutFrontmatter"/> — a test that reimplemented the rule
/// would only prove two implementations agree today, and the criterion is precisely that a file
/// taken from here behaves the same run by this product or by a local agent runner.
/// </para>
/// </summary>
public class StarterCatalogue_Should_Constraint
{
    public static TheoryData<string, string> EveryStarter()
    {
        var data = new TheoryData<string, string>();

        foreach (var tier in StarterCatalogue.Tiers)
        {
            foreach (var prompt in tier.Prompts)
            {
                data.Add(tier.Id, prompt.File);
            }
        }

        return data;
    }

    static StarterPrompt Find(string tierId, string file) =>
        StarterCatalogue
            .Tiers.Single(tier => tier.Id == tierId)
            .Prompts.Single(prompt => prompt.File == file);

    [Theory]
    [MemberData(nameof(EveryStarter))]
    public void EveryStarter_Should_LoadWithABodyAfterFrontmatterIsStripped(
        string tierId,
        string file
    )
    {
        var prompt = Find(tierId, file);

        prompt.Content.ShouldNotBeNullOrWhiteSpace();
        PromptText.WithoutFrontmatter(prompt.Content).ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(EveryStarter))]
    public void EveryStarter_Should_CarryFrontmatterThatIsActuallyStripped(
        string tierId,
        string file
    )
    {
        // Separate from the case above deliberately: a file with no `---` block would satisfy
        // "has a body after stripping" while failing the criterion the stripping exists for. The
        // promise is that the file carries the frontmatter another runner reads and that this
        // product drops it — both halves, or neither is true.
        var prompt = Find(tierId, file);

        prompt.Content.TrimStart().ShouldStartWith("---");

        var body = PromptText.WithoutFrontmatter(prompt.Content);
        body.ShouldNotStartWith("---");
        body.Length.ShouldBeLessThan(prompt.Content.Length);
    }

    [Theory]
    [MemberData(nameof(EveryStarter))]
    public void EveryStarter_Should_SayWhatItIsForAndWhatItAssumes(string tierId, string file)
    {
        // The offer's whole value over a link to a repository: one sentence of purpose, and the
        // capability the prompt still needs. An entry missing either is an entry somebody takes and
        // then discovers by failure.
        var prompt = Find(tierId, file);

        prompt.Purpose.ShouldNotBeNullOrWhiteSpace();
        prompt.Assumes.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TheTiers_Should_BeLabelledByWhatTheyRequire()
    {
        var tiers = StarterCatalogue.Tiers;

        tiers.ShouldNotBeEmpty();

        // Every tier declares what it assumes, or declares that it assumes nothing. This is design
        // D2's entire point: presenting a tier as though it needed only the repository moves the
        // failure from a sentence on the screen to an agent that cannot find a file. #269 gave the
        // sentence a second job — it is the text of the consent that installs the tier — so an empty
        // string is as wrong as a lie.
        tiers.ShouldAllBe(tier => tier.Requires == null || tier.Requires.Length > 0);

        // A tier that needs documents beyond the repository declares them, so the consent can name
        // every path it writes rather than gesturing at "some files".
        tiers
            .Where(tier => tier.Requires != null)
            .ShouldAllBe(tier => tier.Prerequisites.Count > 0);
    }

    [Fact]
    public void EveryPrerequisite_Should_LoadWithABody()
    {
        // The same guarantee starters have, for the same reason: a prerequisite offered as working and
        // shipped empty is worse than none, because the workflow installs and then fails on a file
        // that exists. Enumerating them here is also what turns a manifest entry naming an unembedded
        // file into a red build rather than a runtime surprise.
        var prerequisites = StarterCatalogue.Tiers.SelectMany(tier => tier.Prerequisites).ToList();

        prerequisites.ShouldNotBeEmpty();

        foreach (var prerequisite in prerequisites)
        {
            prerequisite.Path.ShouldNotBeNullOrWhiteSpace();
            prerequisite.Content.ShouldNotBeNullOrWhiteSpace(prerequisite.Path);

            // Repository-relative and nowhere else: an absolute path or a traversal would write
            // outside the workspace the installer prepared.
            prerequisite.Path.ShouldNotStartWith("/");
            prerequisite.Path.ShouldNotContain("..");
        }
    }

    [Fact]
    public void TheCatalogue_Should_NotClaimOnePathTwiceWithinATier()
    {
        // Two entries for one path would write it twice, and the first write would make the second's
        // absence check lie — an existing-file rule that reports a file it created itself.
        foreach (var tier in StarterCatalogue.Tiers)
        {
            tier.Prerequisites.Select(prerequisite => prerequisite.Path)
                .Distinct(StringComparer.Ordinal)
                .Count()
                .ShouldBe(tier.Prerequisites.Count, tier.Id);
        }
    }

    [Fact]
    public void TheCatalogue_Should_NotOfferTheSameFileTwiceInATier()
    {
        // A duplicate would make the collision report ambiguous and the copy control silently pick
        // one of two contents.
        foreach (var tier in StarterCatalogue.Tiers)
        {
            tier.Prompts.Select(prompt => prompt.File)
                .Distinct(StringComparer.Ordinal)
                .Count()
                .ShouldBe(tier.Prompts.Count, tier.Id);
        }
    }

    [Fact]
    public void TheWiring_Should_BeConsistentCatalogueContent()
    {
        // #212 — the default Automations set-up-defaults creates. Two rules keep the wiring
        // honest: no two wired starters may share a trigger under BR-003's case-insensitive
        // identity, and no wiring may hand work to its own trigger (the self-firing loop the
        // create endpoint refuses one at a time).
        var wired = StarterCatalogue
            .Tiers.SelectMany(tier => tier.Prompts)
            .Where(prompt => prompt.Automation is not null)
            .ToList();

        wired.ShouldNotBeEmpty();

        // Across the whole catalogue, with **no exception for any tier** (#269). This is the
        // assertion that keeps "which file a step wires" from becoming a function of which tier a
        // caller consented to: one trigger has one prompt, so a consent changes whether a step is
        // installed and never what it points at. An earlier draft of #269 would have needed a
        // gated-claim carve-out here; deleting the portable tier is what made that unnecessary, and
        // this test is where the simplification would be lost if anyone reintroduced one.
        wired
            .Select(prompt => prompt.Automation!.Trigger)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count()
            .ShouldBe(wired.Count);

        foreach (var prompt in wired)
        {
            // No step claims a transition into its own from-stage: that is #115's self-trigger loop,
            // and the endpoint refuses it — so a catalogue that shipped one would have a tier that
            // cannot be installed.
            string.Equals(
                    prompt.Automation!.ToStage,
                    prompt.Automation!.Trigger,
                    StringComparison.OrdinalIgnoreCase
                )
                .ShouldBeFalse();

            // And no mark repeats the claim, which the endpoint also refuses (#310).
            prompt.Automation!.Marks.ShouldAllBe(mark =>
                !string.Equals(mark, prompt.Automation!.ToStage, StringComparison.OrdinalIgnoreCase)
            );
        }

        // The pipeline is wired via claimed transitions since #310: every to-stage names another
        // wired trigger, so installing the tier builds a lifecycle whose stages all have a step —
        // and the board draws the flow the catalogue promised.
        var triggers = new HashSet<string>(
            wired.Select(prompt => prompt.Automation!.Trigger),
            StringComparer.OrdinalIgnoreCase
        );
        wired
            .Select(prompt => prompt.Automation!.ToStage)
            .Where(toStage => toStage is not null)
            .ShouldAllBe(toStage => triggers.Contains(toStage!));
    }

    [Fact]
    public void TheSpecFirstTier_Should_ArriveAsOneGatedChain()
    {
        // #273 — the methodology decision #269 deferred, pinned as the content it was made as:
        // grill hands to propose, propose to implement, implement to sync, and every step that
        // executes against a repository keeps its approval gate, so the chain's human waits are
        // the gates rather than breaks. refine and status hand to nobody — one is an occasional
        // post-merge append, the other a query, and wiring either in would run it on every pass.
        var workflow = StarterCatalogue.Tiers.Single(tier => tier.Id == "workflow");
        var byFile = workflow.Prompts.ToDictionary(prompt => prompt.File);

        // Claimed transitions since #310, not output labels — so installing this tier is what gives
        // a new project the lifecycle `ai:grill → ai:propose → ai:implement → ai:sync`, as a
        // consequence of each step claiming rather than of anything seeding a default (design D10).
        byFile["grill.md"].Automation!.ToStage.ShouldBe("ai:propose");
        byFile["propose.md"].Automation!.ToStage.ShouldBe("ai:implement");
        byFile["implement.md"].Automation!.ToStage.ShouldBe("ai:sync");
        byFile["sync.md"].Automation!.ToStage.ShouldBeNull();
        byFile["refine.md"].Automation!.ToStage.ShouldBeNull();
        byFile["status.md"].Automation!.ToStage.ShouldBeNull();

        // The only mark the catalogue applies is the hold (DEC-067). It invents no others: a mark
        // names no stage and moves nothing, and no catalogue step needs one.
        workflow
            .Prompts.Where(prompt => prompt.Automation is not null)
            .ShouldAllBe(prompt => prompt.Automation!.Marks.All(StoryHold.Is));

        // Where the chain's hand-offs stop for a person. Losing one silently would turn an
        // attended hand-off into an unattended execution, which is the opposite of what was
        // decided — the wait moved from inside the Run to the Story it finishes with, it did not
        // go away.
        StoryHold.IsHeld(byFile["propose.md"].Automation!.Marks).ShouldBeTrue();
        StoryHold.IsHeld(byFile["implement.md"].Automation!.Marks).ShouldBeTrue();
        StoryHold.IsHeld(byFile["sync.md"].Automation!.Marks).ShouldBeTrue();

        // And the steps that never stopped still do not: grill hands straight on, refine and
        // status are not on the chain at all.
        StoryHold.IsHeld(byFile["grill.md"].Automation!.Marks).ShouldBeFalse();
    }
}
