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
        return (await created.Content.ReadFromJsonAsync<ProjectResponse>())!.Id;
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
    public async Task ThreeKindsOfWaiting_Should_ShareOneList()
    {
        // A failure that nobody has re-triggered.
        AgentSays(false, "boom");
        var failed = await Run("1", _refineId);
        await Execute(failed);

        // A question awaiting its answer.
        AgentSays(true, "What is out of scope?");
        var asking = await Run("2", _grillId);
        await Execute(asking);

        var inbox = await Inbox();

        inbox.Count.ShouldBe(2);
        inbox.Select(entry => entry.WaitingFor).ShouldBe(["input", "failure"]);
        inbox.Single(entry => entry.WaitingFor == "failure").StoryTitle.ShouldBe("First story");
        inbox.ShouldAllBe(entry => entry.ProjectId == _projectId);
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
        AgentSays(true, "Which actor?");
        var asking = await Run("1", _grillId);
        await Execute(asking);
        (await Inbox()).Count.ShouldBe(1);

        // The human answers; the resume checker requeues — the wait is over.
        fixture.Vendor.StoryComments.Add(
            ("1", new StoryComment("The admin.", DateTimeOffset.UtcNow.AddSeconds(5)))
        );
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            await ResumeChecker.CheckOnce(scope.ServiceProvider, CancellationToken.None);
        }

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
        AgentSays(true, "Which actor?");
        var asking = await Run("1", _grillId);
        await Execute(asking);
        (await Inbox()).Count.ShouldBe(1);

        (
            await _client.PostAsync($"/api/projects/{_projectId}/runs/{asking}/cancel", null)
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
        string VendorStoryId,
        string? StoryTitle,
        string WaitingFor,
        DateTimeOffset WaitingSince
    );
}
