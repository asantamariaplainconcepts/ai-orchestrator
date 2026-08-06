using System.Net.Http.Json;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// #190 — the starter set, offered against a project.
/// <para>
/// The assertions worth having here are the ones the catalogue's own unit tests cannot make: that
/// the offer is <i>about this project</i> — where each starter would go, and which ones are already
/// there — and that asking for it writes nothing.
/// </para>
/// </summary>
[Collection(ProjectsCollection.Name)]
public class StarterPromptsEndpoint_Should_Constraint(ProjectsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;

    public async Task InitializeAsync()
    {
        await fixture.ResetDatabase();

        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        created.EnsureSuccessStatusCode();
        _projectId = (await created.Content.ReadFromJsonAsync<ProjectResponse>())!.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task<IReadOnlyList<Tier>> Starters()
    {
        var response = await _client.GetAsync($"/api/projects/{_projectId}/starter-prompts");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<Tier>>())!;
    }

    [Fact]
    public async Task TheSet_Should_ArriveInTiersLabelledByWhatTheyRequire()
    {
        var tiers = await Starters();

        // One tier is a lawful catalogue (#269): what this requirement fixes is that a tier's
        // assumptions are declared, not how many tiers ship. Asserting a count here is what made an
        // earlier version of this test fail for the right change.
        tiers.ShouldNotBeEmpty();
        tiers.ShouldAllBe(tier => tier.Prompts.Count > 0);

        // Every tier either says what it needs or says it needs nothing. A tier that reads documents
        // a fresh repository has not got must not be presented as though it assumed only the
        // repository — one undifferentiated list would hand somebody a prompt that fails on a file.
        tiers.ShouldAllBe(tier => tier.Requires == null || tier.Requires.Length > 0);

        // Every entry carries what makes it usable without a second call.
        tiers
            .SelectMany(tier => tier.Prompts)
            .ShouldAllBe(prompt =>
                prompt.Purpose.Length > 0 && prompt.Assumes.Length > 0 && prompt.Content.Length > 0
            );
    }

    [Fact]
    public async Task EachStarter_Should_SayWhereItWouldGoInThisProject()
    {
        fixture.Documents.Directory = "prompts/ours";

        var prompts = (await Starters()).SelectMany(tier => tier.Prompts).ToList();

        // The path a Run would resolve, because it came from the same prompt read a Run performs —
        // not from this slice composing a second opinion about where prompts live.
        prompts.ShouldAllBe(prompt => prompt.TargetPath!.StartsWith("prompts/ours/"));
        prompts.ShouldContain(prompt => prompt.TargetPath == "prompts/ours/aio-grill.md");
    }

    [Fact]
    public async Task AStarterTheProjectAlreadyHas_Should_BeReported()
    {
        fixture.Documents.Documents["ai/prompts/aio-grill.md"] = "Their own version.";

        var prompts = (await Starters()).SelectMany(tier => tier.Prompts).ToList();

        var present = prompts.Where(prompt => prompt.AlreadyPresent == true).ToList();
        present.Count.ShouldBe(1);
        present.Single().SaveAs.ShouldBe("aio-grill.md");

        // And every other one is a definite "no", not a shrug — the distinction only holds because
        // the read reached the vendor for all of them.
        prompts
            .Where(prompt => prompt.SaveAs != "aio-grill.md")
            .ShouldAllBe(prompt => prompt.AlreadyPresent == false);
    }

    [Fact]
    public async Task WithNoConnector_Should_ReadAsUnknownRatherThanAbsent()
    {
        // Looking at the catalogue before configuring a Connector is an ordinary first step, so the
        // set still arrives. What it must not do is claim the project does not have these files —
        // nothing looked. Null, not false; the same distinction BR-011 makes about an unmeasured cost.
        fixture.Documents.Connected = false;

        var prompts = (await Starters()).SelectMany(tier => tier.Prompts).ToList();

        prompts.ShouldNotBeEmpty();
        prompts.ShouldAllBe(prompt => prompt.AlreadyPresent == null && prompt.TargetPath == null);
    }

    [Fact]
    public async Task AskingForTheSet_Should_CostOneReadPerStarterAndNothingElse()
    {
        // "It writes nothing" is guaranteed by the type rather than by this test — the handler's only
        // seam is IDocumentReader, which has no write on it, so a write would not compile. Asserting
        // an empty write log would be asserting that nobody seeded one.
        //
        // What is worth asserting is the part a type cannot hold: the vendor traffic is bounded by
        // the catalogue's size, not the repository's, and it is exactly the collision read.
        var starters = (await Starters()).SelectMany(tier => tier.Prompts).ToList();

        fixture.Documents.Reads.Count.ShouldBe(starters.Count);
        fixture.Documents.Reads.ShouldBe(
            [.. starters.Select(prompt => prompt.SaveAs)],
            ignoreOrder: true
        );
    }

    [Fact]
    public async Task NoTwoStarters_Should_LandOnTheSamePath()
    {
        // Found while writing these tests, not by reading the manifest: two tiers both shipped an
        // implement.md, and without distinct saved names they resolved to one path — so taking both
        // was impossible and the collision report would have marked two entries present for one
        // file. #269 left one tier standing, which removes the instance and not the rule: `saveAs`
        // is still what keeps a starter off a path a team's own file may occupy.
        var paths = (await Starters())
            .SelectMany(tier => tier.Prompts)
            .Select(prompt => prompt.TargetPath)
            .ToList();

        paths.Distinct(StringComparer.Ordinal).Count().ShouldBe(paths.Count);
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record Tier(
        string Id,
        string Title,
        string Summary,
        string? Requires,
        IReadOnlyList<Prompt> Prompts
    );

    sealed record Prompt(
        string File,
        string SaveAs,
        string Purpose,
        string Assumes,
        string Content,
        string? TargetPath,
        bool? AlreadyPresent
    );
}
