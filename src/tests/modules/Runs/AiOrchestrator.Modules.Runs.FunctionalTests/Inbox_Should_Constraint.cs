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
    Guid _approvalId;

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

        _refineId = await CreateAutomation(_projectId, "ai:refine", "RepositoryPrompt", false);
        _grillId = await CreateAutomation(_projectId, "ai:grill", "RepositoryPrompt", false);
        _approvalId = await CreateAutomation(
            _projectId,
            "ai:implement",
            "RepositoryPrompt",
            requiresApproval: true
        );

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

    async Task<Guid> CreateAutomation(
        Guid projectId,
        string trigger,
        string action,
        bool requiresApproval
    )
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
                requiresApproval,
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
        // Two kinds now, not three (#162, design D5): the grill's question path was the only
        // producer of "input", and it left with the catalogue. The category still exists in the
        // vocabulary and nothing enters it — kept dormant deliberately, because changing Run states
        // was out of scope. This test asserts the two a Run can still reach.

        // A failure that nobody has re-triggered.
        AgentSays(false, "boom");
        var failed = await Run("1", _refineId);
        await Execute(failed);

        // A plan awaiting its human.
        AgentSays(true, "The plan.");
        var planning = await Run("2", _approvalId);
        await Execute(planning);

        var inbox = await Inbox();

        inbox.Count.ShouldBe(2);
        inbox.Select(entry => entry.WaitingFor).ShouldBe(["approval", "failure"]);
        inbox.Single(entry => entry.WaitingFor == "failure").StoryTitle.ShouldBe("First story");
        inbox.ShouldAllBe(entry => entry.ProjectId == _projectId);
        // Cross-project by design (UC-026), so every row names its Project — "which #491?"
        // must never be the reader's problem.
        inbox.ShouldAllBe(entry => entry.ProjectName == _projectName);
    }

    [Fact]
    public async Task AnApprovalWait_Should_AppearAsApproval()
    {
        AgentSays(true, "The plan.");
        var planning = await Run("1", _approvalId);
        await Execute(planning);

        var inbox = await Inbox();

        inbox.Single().WaitingFor.ShouldBe("approval");
    }

    [Fact]
    public async Task Resolution_Should_RemoveTheEntry()
    {
        // The producible wait is the approval now (#162): the human acting on the entry is what
        // clears it, which is the property this test has always been about — the actor changed,
        // the rule did not.
        AgentSays(true, "The plan.");
        var planning = await Run("1", _approvalId);
        await Execute(planning);
        (await Inbox()).Count.ShouldBe(1);

        (
            await _client.PostAsync($"/api/projects/{_projectId}/runs/{planning}/reject", null)
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
    public async Task ACancelledWait_Should_LeaveTheInbox()
    {
        AgentSays(true, "The plan.");
        var waiting = await Run("1", _approvalId);
        await Execute(waiting);
        (await Inbox()).Count.ShouldBe(1);

        (
            await _client.PostAsync($"/api/projects/{_projectId}/runs/{waiting}/cancel", null)
        ).EnsureSuccessStatusCode();

        // Cancelled is terminal-by-human-choice: it waits on nobody and must not linger as a
        // failure either.
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
