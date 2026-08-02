using System.Net.Http.Json;
using System.Text.Json;
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

    [Fact]
    public async Task AFreshProject_Should_GainTheWiredSetOnce()
    {
        var first = await Invoke();

        // The wired portable set, created and named. The exact triggers are catalogue content;
        // what this asserts is the contract: everything created, nothing skipped, and the
        // pipeline edge the catalogue promises (implement hands to tests) really stored.
        first.GetProperty("created").GetArrayLength().ShouldBeGreaterThanOrEqualTo(5);
        first.GetProperty("skipped").GetArrayLength().ShouldBe(0);

        var automations = await Automations();
        var implement = automations
            .EnumerateArray()
            .Single(automation =>
                automation.GetProperty("triggerLabel").GetString() == "ai:implement"
            );
        implement.GetProperty("requiresApproval").GetBoolean().ShouldBeTrue();
        implement
            .GetProperty("outputLabels")
            .EnumerateArray()
            .Select(label => label.GetString())
            .ShouldContain("ai:tests");
        implement.GetProperty("enabled").GetBoolean().ShouldBeTrue();

        // No Connector on this project: every created Automation's prompt is unreadable, and
        // the action says so per file instead of writing anything anywhere (#190 design D1).
        first
            .GetProperty("missingPrompts")
            .GetArrayLength()
            .ShouldBe(first.GetProperty("created").GetArrayLength());

        // Idempotence: the second run creates nothing and names everything as already there.
        var second = await Invoke();
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
                requiresApproval = false,
            }
        );
        existing.EnsureSuccessStatusCode();

        var result = await Invoke();

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

    async Task<JsonElement> Invoke()
    {
        var response = await _client.PostAsync(
            $"/api/projects/{_projectId}/automations/set-up-defaults",
            null
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
