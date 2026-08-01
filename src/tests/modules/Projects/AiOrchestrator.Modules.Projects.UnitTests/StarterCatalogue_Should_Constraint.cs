using AiOrchestrator.BuildingBlocks.Agents;
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

        // Portable first, because it is the tier that answers "this project has no prompts". A
        // surface that led with the methodology would be offering a way of working to somebody who
        // asked for a starting point.
        tiers.First().Id.ShouldBe("portable");
        tiers.First().Requires.ShouldBeNull();

        // And the other tier says what it needs. This is design D2's entire point: the reference set
        // this change started from was five-sixths unportable, and presenting it as though it were
        // portable moves the failure from a sentence on the screen to an agent that cannot find a
        // file.
        tiers
            .Where(tier => tier.Id != "portable")
            .ShouldAllBe(tier => tier.Requires != null && tier.Requires.Length > 0);
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

        wired
            .Select(prompt => prompt.Automation!.Trigger)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count()
            .ShouldBe(wired.Count);

        foreach (var prompt in wired)
        {
            prompt.Automation!.OutputLabels.ShouldAllBe(label =>
                !string.Equals(
                    label,
                    prompt.Automation!.Trigger,
                    StringComparison.OrdinalIgnoreCase
                )
            );
        }

        // The pipeline is wired via output labels: every label handed on that is meant to chain
        // must name another wired trigger, so the board draws the flow the catalogue promised.
        var triggers = new HashSet<string>(
            wired.Select(prompt => prompt.Automation!.Trigger),
            StringComparer.OrdinalIgnoreCase
        );
        wired
            .SelectMany(prompt => prompt.Automation!.OutputLabels)
            .ShouldAllBe(label => triggers.Contains(label));
    }
}
