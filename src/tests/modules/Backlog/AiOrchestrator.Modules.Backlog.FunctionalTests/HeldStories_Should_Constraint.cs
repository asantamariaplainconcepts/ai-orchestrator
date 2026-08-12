using System.Net.Http.Json;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Backlog.Domain;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.FunctionalTests;

/// <summary>
/// #335 — the mirror answers "which of this project's Stories are held" in one read, for the
/// sidebar tree.
/// <para>
/// Functional rather than unit, deliberately. <c>StoryHold.IsHeld</c>'s case fold is already covered
/// by its own unit tests; what is untested is this read against a <b>real</b> Postgres, where
/// <c>Labels</c> is a <c>text[]</c> column. That is exactly where the plausible mistake lives — a
/// translated <c>Contains</c> would pass a unit test over an in-memory list and then miss <c>HITL</c>
/// in production.
/// </para>
/// </summary>
[Collection(BacklogCollection.Name)]
public class HeldStories_Should_Constraint(BacklogApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    readonly Guid _projectId = Guid.CreateVersion7();

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        await fixture.ResetDatabase();

        (
            await _client.PutAsJsonAsync(
                $"/api/projects/{_projectId}/connector",
                new
                {
                    owner = "acme",
                    repository = "portal",
                    secretName = "acme-pat",
                }
            )
        ).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Seeds the vendor and drives the real reconciliation into the mirror.</summary>
    async Task Mirror(params VendorStory[] stories)
    {
        fixture.Vendor.Stories.AddRange(stories);
        (
            await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", content: null)
        ).EnsureSuccessStatusCode();
    }

    async Task<IReadOnlyList<HeldStory>> Held(Guid? projectId = null)
    {
        using var scope = fixture.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IStoryReader>();
        return await reader.Held(projectId ?? _projectId);
    }

    [Fact]
    public async Task Held_Should_ReturnOnlyTheHeldStories()
    {
        await Mirror(
            new VendorStory("1", "Add login", "open", ["hitl"]),
            new VendorStory("2", "Fix the header", "open", ["bug"]),
            new VendorStory("3", "Untouched", "open", [])
        );

        var held = await Held();

        held.Select(story => story.VendorStoryId).ShouldBe(["1"]);
        held.Single().Title.ShouldBe("Add login");
    }

    /// <summary>
    /// The reason this read filters in memory. Against a <c>text[]</c> column a translated
    /// containment test is case-sensitive, so this is the case that would silently regress if
    /// somebody "optimised" the filter into SQL (DEC-056).
    /// </summary>
    [Fact]
    public async Task Held_Should_FoldCaseTheWayTheVendorDoes()
    {
        await Mirror(
            new VendorStory("1", "Lower", "open", ["hitl"]),
            new VendorStory("2", "Upper", "open", ["HITL"]),
            new VendorStory("3", "Mixed", "open", ["Hitl"]),
            new VendorStory("4", "Padded", "open", [" hitl "])
        );

        var held = await Held();

        held.Select(story => story.VendorStoryId).OrderBy(id => id).ShouldBe(["1", "2", "3", "4"]);
    }

    [Fact]
    public async Task Held_Should_BeEmptyWhenNothingIsHeld()
    {
        await Mirror(new VendorStory("1", "Add login", "open", ["bug"]));

        (await Held()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Held_Should_BeEmptyForAProjectWithNoMirror()
    {
        (await Held(Guid.CreateVersion7())).ShouldBeEmpty();
    }

    /// <summary>
    /// A hold alongside other labels is still a hold — the vendor's label set is not a single-slot
    /// field, and a Story in flight will usually carry a trigger label too.
    /// </summary>
    [Fact]
    public async Task Held_Should_RecogniseTheHoldAmongOtherLabels()
    {
        await Mirror(new VendorStory("1", "Add login", "open", ["bug", "hitl", "ai:implement"]));

        (await Held()).Single().VendorStoryId.ShouldBe("1");
    }

    /// <summary>One project's hold is not another's — the read is scoped, like every other.</summary>
    [Fact]
    public async Task Held_Should_NotCrossProjects()
    {
        await Mirror(new VendorStory("1", "Add login", "open", ["hitl"]));

        var other = Guid.CreateVersion7();
        (
            await _client.PutAsJsonAsync(
                $"/api/projects/{other}/connector",
                new
                {
                    owner = "acme",
                    repository = "other",
                    secretName = "acme-pat",
                }
            )
        ).EnsureSuccessStatusCode();

        (await Held(other)).ShouldBeEmpty();
    }
}
