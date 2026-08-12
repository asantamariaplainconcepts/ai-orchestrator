using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Domain;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// #212 — the starter pipeline in one action. The property that matters is convergence: after
/// the action the wired set exists exactly once, whatever existed before and however many times
/// it runs. Permission enforcement is the declared <c>[Requires]</c> the CQS pipeline carries,
/// covered by the authorization suite — what is asserted here is what this action does.
/// </summary>
[Collection(ProjectsCollection.Name)]
public class SetUpDefaultAutomations_Should_Constraint(ProjectsApiFixture fixture) : IAsyncLifetime
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

    /// <summary>The stored lifecycle, read through the endpoint that serves it — never off the table,
    /// so what a client would see is what is asserted.</summary>
    async Task<IReadOnlyList<string>> Stages()
    {
        var response = await _client.GetAsync($"/api/projects/{_projectId}/lifecycle");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return
        [
            .. document
                .RootElement.GetProperty("stages")
                .EnumerateArray()
                .Select(stage => stage.GetString()!),
        ];
    }

    [Fact]
    public async Task ABodylessCall_Should_ConsentToNothing()
    {
        // **A behaviour change, asserted rather than discovered** (#269). Before consent existed, a
        // bodyless call created the whole portable set. The catalogue now ships one tier and that tier
        // declares a prerequisite, so a caller who names no tier authorises nothing — and this project
        // has no Connector, so nothing is adopted either. Nothing is created, and that is correct:
        // an Automation naming a prompt that does not exist and will not be installed is the
        // configurable thing that silently never fires.
        var result = await Invoke();

        result.GetProperty("created").GetArrayLength().ShouldBe(0);
        result.GetProperty("skipped").GetArrayLength().ShouldBe(0);
        (await Automations()).GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task InstallingATier_Should_GiveTheProjectALifecycle()
    {
        // #310 design D10, and the reason "seed a default lifecycle" could stay out of scope: a stage
        // exists only as a consequence of an Automation claiming a transition that names it, so a
        // project that installs the spec-first tier acquires the tier's flow as its lifecycle without
        // anything seeding one.
        //
        // Exercised rather than reasoned about (ADR-0001): a fresh project's lifecycle is empty, and
        // the assertion is the stored order after the install — which is also the order the board will
        // draw its columns in, since nothing derives that any more.
        (await Stages()).ShouldBeEmpty();

        await Invoke(tiers: ["workflow"]);

        // The chain the catalogue claims, in order. `ai:refine` and `ai:status` claim nothing, so they
        // contribute no stage — a trigger somebody applies on its own is not a place a Story sits.
        (await Stages()).ShouldBe(["ai:grill", "ai:propose", "ai:implement", "ai:sync"]);

        // Idempotent, like the install itself: a second pass claims nothing new, so the lifecycle is
        // untouched rather than accumulating a second spelling of every stage.
        await Invoke(tiers: ["workflow"]);
        (await Stages()).ShouldBe(["ai:grill", "ai:propose", "ai:implement", "ai:sync"]);
    }

    [Fact]
    public async Task AFreshProject_Should_GainTheWiredSetOnce()
    {
        var first = await Invoke(tiers: ["workflow"]);

        // The wired set, created and named. The exact triggers are catalogue content; what this
        // asserts is the contract: everything created, nothing skipped, and the wiring really stored.
        first.GetProperty("created").GetArrayLength().ShouldBeGreaterThanOrEqualTo(5);
        first.GetProperty("skipped").GetArrayLength().ShouldBe(0);

        var automations = await Automations();
        var implement = automations
            .EnumerateArray()
            .Single(automation =>
                automation.GetProperty("triggerLabel").GetString() == "ai:implement"
            );

        // ai:implement is wired to the tier's own file now that nothing contends for the trigger
        // (#269), and it still waits for a person — the step that writes code holds the Story when
        // it finishes, so nothing runs on until somebody has looked (#321, DEC-067). The wait moved
        // from inside its Run to the boundary after it; the shipped chain still stops here.
        implement
            .GetProperty("outputLabels")
            .EnumerateArray()
            .Select(label => label.GetString())
            .ShouldContain(StoryHold.Label);
        implement.GetProperty("promptPath").GetString().ShouldBe("aio-implement.md");
        implement.GetProperty("enabled").GetBoolean().ShouldBeTrue();

        // No Connector on this project: every created Automation's prompt is unreadable, and
        // the action says so per file instead of writing anything anywhere (#190 design D1).
        first
            .GetProperty("missingPrompts")
            .GetArrayLength()
            .ShouldBe(first.GetProperty("created").GetArrayLength());

        // Idempotence: the second run creates nothing and names everything as already there.
        var second = await Invoke(tiers: ["workflow"]);
        second.GetProperty("created").GetArrayLength().ShouldBe(0);
        second
            .GetProperty("skipped")
            .GetArrayLength()
            .ShouldBe(first.GetProperty("created").GetArrayLength());
        (await Automations())
            .GetArrayLength()
            .ShouldBe(first.GetProperty("created").GetArrayLength());
    }

    [Fact]
    public async Task AnUnknownTier_Should_MatchNothing()
    {
        // A name the catalogue does not contain is not an error and installs nothing — the same
        // forgiveness the selection gives an unknown trigger.
        var result = await Invoke(tiers: ["no-such-tier"]);

        result.GetProperty("created").GetArrayLength().ShouldBe(0);
        (await Automations()).GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task AnExistingTrigger_Should_BeSkippedWhateverItsCase()
    {
        // The BR-003 identity (DEC-056): AI:IMPLEMENT and ai:implement are one trigger, so the
        // starter for it is skipped — and everything else is still created.
        var existing = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "AI:IMPLEMENT",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "mine.md",
            }
        );
        existing.EnsureSuccessStatusCode();

        var result = await Invoke(tiers: ["workflow"]);

        result
            .GetProperty("skipped")
            .EnumerateArray()
            .Select(entry => entry.GetProperty("trigger").GetString())
            .ShouldContain("ai:implement");
        result
            .GetProperty("created")
            .EnumerateArray()
            .Select(trigger => trigger.GetString())
            .ShouldNotContain("ai:implement");
        result.GetProperty("created").GetArrayLength().ShouldBeGreaterThanOrEqualTo(4);

        // The Admin's own Automation is untouched — convergence never edits what exists.
        var automations = await Automations();
        automations
            .EnumerateArray()
            .Single(automation =>
                string.Equals(
                    automation.GetProperty("triggerLabel").GetString(),
                    "ai:implement",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .GetProperty("promptPath")
            .GetString()
            .ShouldBe("mine.md");
    }

    /// <summary>
    /// No argument posts <b>no body at all</b>, which is the #212 call and, since #269, a call that
    /// consents to nothing. Passing tiers sends the consent.
    /// </summary>
    async Task<JsonElement> Invoke(IReadOnlyList<string>? tiers = null)
    {
        var response = tiers is null
            ? await _client.PostAsync(
                $"/api/projects/{_projectId}/automations/set-up-defaults",
                null
            )
            : await _client.PostAsJsonAsync(
                $"/api/projects/{_projectId}/automations/set-up-defaults",
                new { tiers }
            );
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    async Task<JsonElement> Automations()
    {
        var response = await _client.GetStringAsync($"/api/projects/{_projectId}/automations");
        return JsonDocument.Parse(response).RootElement.Clone();
    }

    sealed record ProjectResponse(Guid Id, string Name);
}
