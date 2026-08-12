using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Domain;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// BR-007 against real containers (#321, DEC-067): while a Story carries the hold, nothing starts.
/// <para>
/// The negatives use the same delivery-probe fence <see cref="RunMatching_Should_Constraint"/> uses
/// — a recorded delivery means the Runs handler already finished, so "no Run exists" is a fact
/// rather than a race. Asserting the absence of a Run without it would pass while the handler was
/// still running and prove nothing.
/// </para>
/// </summary>
[Collection(RunsCollection.Name)]
public class StoryHold_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        await fixture.ResetDatabase();
        await fixture.ResetQueue();

        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        created.EnsureSuccessStatusCode();
        _projectId = (await created.Content.ReadFromJsonAsync<ProjectResponse>())!.Id;

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

    async Task<Guid> AddAutomation(string label, string? toStage = null)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = label,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "story.md",
                toStage,
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AutomationCreated>())!.Id;
    }

    Task Refresh() => _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);

    async Task<List<RunState>> RunStates()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        return await database
            .Runs.Where(run => run.ProjectId == _projectId)
            .Select(run => run.State)
            .ToListAsync();
    }

    [Fact]
    public async Task AHeldStory_Should_MatchNothing()
    {
        await AddAutomation("run-me");
        fixture.Vendor.Stories.Add(
            new VendorStory("1", "Add login", "open", ["run-me", StoryHold.Label])
        );

        await Refresh();
        // The fence: the handler has seen this Story and finished with it.
        await fixture.Probe.WaitForAtLeast(_projectId, 1);

        (await RunStates()).ShouldBeEmpty();
        (await fixture.DispatchedRunIds()).ShouldBeEmpty();
    }

    [Fact]
    public async Task AHoldSpelledInAnotherCase_Should_StillHold()
    {
        // DEC-056: the vendor treats labels case-insensitively and so does BR-003's identity. An
        // Admin who typed the hold in the vendor's own casing must not watch the flow run past it.
        await AddAutomation("run-me");
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", ["run-me", "HITL"]));

        await Refresh();
        await fixture.Probe.WaitForAtLeast(_projectId, 1);

        (await RunStates()).ShouldBeEmpty();
    }

    [Fact]
    public async Task RunNowOnAHeldStory_Should_BeRefusedNamingTheHold()
    {
        // BR-013: manual dispatch bypasses detection, never a rule. This is the case the refusal
        // in RunCreator exists for — the endpoint never goes near the matching handler.
        var automationId = await AddAutomation("run-me");
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", [StoryHold.Label]));
        await Refresh();

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "1", automationId }
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadAsStringAsync();
        problem.ShouldContain(StoryHold.Label);

        (await RunStates()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ClearingTheHold_Should_LetTheNextStepStart()
    {
        // The whole of "no resume machinery": removing the label produces an ordinary story event,
        // and matching does what it always does (BR-015).
        await AddAutomation("run-me");
        var held = new VendorStory("1", "Add login", "open", ["run-me", StoryHold.Label]);
        fixture.Vendor.Stories.Add(held);

        await Refresh();
        await fixture.Probe.WaitForAtLeast(_projectId, 1);
        (await RunStates()).ShouldBeEmpty();

        fixture.Vendor.Stories.Remove(held);
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", ["run-me"]));

        await Refresh();
        await fixture.Probe.WaitForAtLeast(_projectId, 2);

        (await RunStates()).ShouldHaveSingleItem().ShouldBe(RunState.Queued);
    }

    [Fact]
    public async Task AStoryHeldWithNoTriggerLabel_Should_StayThatWay()
    {
        // A hold is not a trigger: it stops work, it never starts any.
        await AddAutomation("run-me");
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", [StoryHold.Label]));

        await Refresh();
        await fixture.Probe.WaitForAtLeast(_projectId, 1);

        (await RunStates()).ShouldBeEmpty();
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationCreated(Guid Id);
}
