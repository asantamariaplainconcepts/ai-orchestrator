using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Features.Conversation;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// UC-026. The property that keeps an inbox alive is subtraction: entries leave when the human
/// has acted — including the derived case, a failure whose Story already has a newer Run.
/// </summary>
[Collection(RunsCollection.Name)]
public class Inbox_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;
    string _projectName = "";
    Guid _refineId;
    Guid _grillId;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Agent.Reset();
        fixture.Workspace.Reset();
        await fixture.ResetDatabase();
        await fixture.ResetQueue();

        _projectId = await CreateProject();

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

        _refineId = await CreateAutomation(_projectId, "ai:refine", "RepositoryPrompt");
        _grillId = await CreateAutomation(_projectId, "ai:grill", "RepositoryPrompt");

        fixture.Vendor.Documents["docs/process/definition-of-ready.md"] = "Needs criteria.";
        fixture.Vendor.Stories.Add(new VendorStory("1", "First story", "open", [], "Body."));
        fixture.Vendor.Stories.Add(new VendorStory("2", "Second story", "open", [], "Body."));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 2);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task<Guid> CreateProject()
    {
        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        created.EnsureSuccessStatusCode();
        var project = (await created.Content.ReadFromJsonAsync<ProjectResponse>())!;
        _projectName = project.Name;
        return project.Id;
    }

    async Task<Guid> CreateAutomation(Guid projectId, string trigger, string action)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/automations",
            new
            {
                triggerLabel = trigger,
                triggerState = (string?)null,
                action,
                runtime = "ClaudeCodeHeadless",
                promptPath = "story.md",
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;
    }

    async Task<Guid> Run(string story, Guid automation)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = story, automationId = automation }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RunNowResponse>())!.Id;
    }

    async Task Execute(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRunExecutor>().Execute(runId);
    }

    async Task<IReadOnlyList<InboxEntry>> Inbox() =>
        (await _client.GetFromJsonAsync<IReadOnlyList<InboxEntry>>("/api/inbox"))!;

    void AgentSays(bool ok, string reply) =>
        fixture.Agent.Result = new AgentResult(ok, reply, null, null);

    [Fact]
    public async Task TheKindsOfWaiting_Should_ShareOneList()
    {
        // One producible kind now, not two (#321, DEC-067). "Input" left with the catalogue
        // (#162) and "approval" left with the plan gate; both categories survive in the
        // vocabulary and the query, and nothing enters either — kept dormant deliberately,
        // because changing Run states was out of scope then and is out of scope now.
        //
        // What this asserts is therefore the list's shape around the one wait a Run can still
        // reach. UC-026's promise is narrower than it was until the follow-up carries held
        // Stories in here, and that narrowing is stated rather than hidden behind a test that
        // seeds a state nothing produces.

        // A failure that nobody has re-triggered.
        AgentSays(false, "boom");
        var failed = await Run("1", _refineId);
        await Execute(failed);

        var inbox = await Inbox();

        inbox.Count.ShouldBe(1);
        inbox.Single().WaitingFor.ShouldBe("failure");
        inbox.Single().StoryTitle.ShouldBe("First story");
        inbox.ShouldAllBe(entry => entry.ProjectId == _projectId);
        // Cross-project by design (UC-026), so every row names its Project — "which #491?"
        // must never be the reader's problem.
        inbox.ShouldAllBe(entry => entry.ProjectName == _projectName);
    }

    [Fact]
    public async Task Resolution_Should_RemoveTheEntry()
    {
        // Subtraction is the property, and it has outlived two of its actors: the grill's answer
        // (#162), then the plan approval (#321). What clears an entry now is a person dismissing
        // a failure — saying they looked and chose not to re-run. The actor changed again; the
        // rule did not.
        AgentSays(false, "boom");
        var failed = await Run("1", _refineId);
        await Execute(failed);
        (await Inbox()).Count.ShouldBe(1);

        (
            await _client.PostAsync($"/api/projects/{_projectId}/runs/{failed}/dismiss", null)
        ).EnsureSuccessStatusCode();

        (await Inbox()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ARetriggeredFailure_Should_WaitOnNobody()
    {
        AgentSays(false, "boom");
        var failed = await Run("1", _refineId);
        await Execute(failed);
        (await Inbox()).Single().WaitingFor.ShouldBe("failure");

        // The human re-triggers (BR-013): a newer Run exists, so the failure leaves the inbox
        // even though the Failed row is immortal (BR-014).
        AgentSays(true, "A helpful comment.");
        var retry = await Run("1", _refineId);
        await Execute(retry);

        (await Inbox()).ShouldBeEmpty();
    }

    [Fact]
    public async Task AnEmptyInbox_Should_BeEmptyNotAnError()
    {
        var response = await _client.GetAsync("/api/inbox");

        response.EnsureSuccessStatusCode();
        (await response.Content.ReadFromJsonAsync<IReadOnlyList<InboxEntry>>())!.ShouldBeEmpty();
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);

    sealed record InboxEntry(
        Guid RunId,
        Guid ProjectId,
        string? ProjectName,
        string VendorStoryId,
        string? StoryTitle,
        string WaitingFor,
        DateTimeOffset WaitingSince
    );
}
