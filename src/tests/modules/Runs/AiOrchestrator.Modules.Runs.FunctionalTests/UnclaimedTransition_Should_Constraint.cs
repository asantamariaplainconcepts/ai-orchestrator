using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #310 task 5.2 — <b>a human step needs no representation</b>, and this is the evidence for it:
/// <c>Features/Matching/StoryChangedHandler.cs</c> is untouched by this change, and the reason is that
/// a person moving a label already travels the mechanism an Automation's label travels.
/// <para>
/// So the test runs the same transition twice into the same downstream Automation — once with the
/// label applied by a person at the vendor, once with it applied by a Run claiming that transition —
/// and compares the Runs that come back. Same Automation, same state, both dispatched: an unclaimed
/// transition is a person's turn (BR-006), not a gap in the wiring, and no dispatch machinery was
/// added for it.
/// </para>
/// <para>
/// That this file adds no production code is the point. A change that had needed a second path for
/// human steps would have had to write one here (design D9); the absence is what makes "nothing fires
/// until a person moves the label" a fact about the model rather than a promise about the UI.
/// </para>
/// </summary>
[Collection(RunsCollection.Name)]
public class UnclaimedTransition_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Agent.Reset();
        fixture.Workspace.Reset();
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

    async Task<Guid> Automation(string trigger, string? toStage)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = trigger,
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "story.md",
                toStage,
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;
    }

    Task Refresh() => _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);

    async Task<List<RunRow>> Runs()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        return await database
            .Runs.Where(run => run.ProjectId == _projectId)
            .Select(run => new RunRow(
                run.Id,
                run.VendorStoryId,
                run.AutomationId,
                run.State.ToString(),
                run.DispatchedAt
            ))
            .ToListAsync();
    }

    [Fact]
    public async Task APersonMovingALabel_Should_ProduceTheRunAnAutomationsLabelWould()
    {
        // The lifecycle under test: 'ai:review' → 'ai:sync' → onwards. The Automation triggered on
        // 'ai:sync' claims nothing further; it is the one both paths must reach.
        var downstream = await Automation("ai:sync", toStage: null);
        // And the same transition claimed by an Automation, for the second half of the comparison.
        var claimant = await Automation("ai:review", toStage: "ai:sync");

        fixture.Vendor.Stories.Add(new VendorStory("1", "Moved by a person", "open", [], "Body."));
        fixture.Vendor.Stories.Add(new VendorStory("2", "Moved by a Run", "open", [], "Body."));

        // Path one: a person applies the label at the vendor. Nothing in this product wrote it, and
        // nothing in this product was asked to.
        var index = fixture.Vendor.Stories.FindIndex(story => story.VendorId == "1");
        fixture.Vendor.Stories[index] = fixture.Vendor.Stories[index] with { Labels = ["ai:sync"] };

        await Refresh();
        await fixture.Probe.WaitForAtLeast(_projectId, 1);

        var byPerson = (await Runs()).Single(run => run.VendorStoryId == "1");

        // Path two: a Run of the claiming Automation applies the same label as its lifecycle move.
        // Dispatched by id (BR-013) rather than by labelling the story, so the only label story 2
        // ever carries is the one the Run wrote — which keeps the second match unambiguous.
        fixture.Agent.Result = new AgentResult(true, "A helpful comment.", null, null);
        var claiming = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "2", automationId = claimant }
        );
        claiming.EnsureSuccessStatusCode();
        var claimingRunId = (await claiming.Content.ReadFromJsonAsync<RunNowResponse>())!.Id;

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRunExecutor>().Execute(claimingRunId);
        }

        fixture
            .Vendor.Stories.Single(story => story.VendorId == "2")
            .Labels.ShouldContain("ai:sync");

        await Refresh();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        var byAutomation = (await Runs()).SingleOrDefault(run =>
            run.VendorStoryId == "2" && run.AutomationId == downstream
        );
        while (byAutomation is null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
            byAutomation = (await Runs()).SingleOrDefault(run =>
                run.VendorStoryId == "2" && run.AutomationId == downstream
            );
        }

        byAutomation.ShouldNotBeNull();

        // The comparison, which is the whole test: two label writers, one mechanism. The Run a person
        // caused and the Run an Automation caused are the same Run in every respect that is about the
        // transition — which is why an unclaimed boundary needs no representation to work.
        byPerson.AutomationId.ShouldBe(downstream);
        byAutomation.AutomationId.ShouldBe(byPerson.AutomationId);
        byAutomation.State.ShouldBe(byPerson.State);
        byPerson.State.ShouldBe(nameof(RunState.Queued));
        byPerson.DispatchedAt.ShouldNotBeNull();
        byAutomation.DispatchedAt.ShouldNotBeNull();
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);

    sealed record RunRow(
        Guid Id,
        string? VendorStoryId,
        Guid? AutomationId,
        string State,
        DateTimeOffset? DispatchedAt
    );
}
