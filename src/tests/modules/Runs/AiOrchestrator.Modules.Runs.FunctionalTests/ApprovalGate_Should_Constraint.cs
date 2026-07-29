using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Dispatch;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// UC-015 + UC-013: the Agent proposes, the Run waits, a human decides. The rules that are
/// easy to break by treating the two phases as one long Run — untimed waiting (BR-006), no cap
/// slot (BR-002), the Story still held (BR-001) — are asserted rather than assumed.
/// </summary>
[Collection(RunsCollection.Name)]
public class ApprovalGate_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;
    Guid _automationId;

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

        var automation = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "ai:review",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                rubricPath = "task.md",
                runtime = "ClaudeCodeHeadless",
                requiresApproval = true,
            }
        );
        automation.EnsureSuccessStatusCode();
        _automationId = (await automation.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;

        fixture.Vendor.Stories.Add(new VendorStory("9", "A story", "open", [], "Do the thing."));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 1);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task<Guid> Dispatch()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "9", automationId = _automationId }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RunNowResponse>())!.Id;
    }

    async Task Execute(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRunExecutor>().Execute(runId);
    }

    async Task<RunRow> Load(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        return await database
            .Runs.Where(run => run.Id == runId)
            .Select(run => new RunRow(
                run.State.ToString(),
                run.Plan,
                run.ApprovedAt,
                run.OutputLink,
                run.EndedAt
            ))
            .SingleAsync();
    }

    async Task<List<Guid>> QueuedRunIds()
    {
        var peeked = await fixture.Queue.PeekMessagesAsync(maxMessages: 32);
        return
        [
            .. peeked.Value.Select(message =>
                DispatchMessage.TryParse(message.MessageText)?.RunId ?? Guid.Empty
            ),
        ];
    }

    Task<HttpResponseMessage> Decide(Guid runId, string decision) =>
        _client.PostAsync($"/api/projects/{_projectId}/runs/{runId}/{decision}", null);

    /// <summary>Phase 1 to the pause — the starting point for most of these tests.</summary>
    async Task<Guid> PauseOnPlan()
    {
        fixture.Agent.Result = new AgentResult(
            Succeeded: true,
            Log: "## Plan\n\nChange the thing.",
            OutputLink: null,
            Usage: null
        );

        var runId = await Dispatch();
        await Execute(runId);
        return runId;
    }

    [Fact]
    public async Task PhaseOne_Should_StoreThePlanAndPublishNothing()
    {
        var runId = await PauseOnPlan();

        var run = await Load(runId);
        run.State.ShouldBe("AwaitingApproval");
        run.Plan!.ShouldContain("Change the thing.");
        run.OutputLink.ShouldBeNull();
        run.EndedAt.ShouldBeNull();

        // A plan-phase pull request would be a lie: the workspace was prepared but never
        // published, and the instruction told the Agent to change nothing.
        fixture.Workspace.Published.ShouldBeFalse();
        fixture.Agent.Instructions.Single().Prompt.ShouldContain("Change nothing");
    }

    [Fact]
    public async Task Approval_Should_ResumeIntoExecutionCarryingThePlan()
    {
        var runId = await PauseOnPlan();
        await fixture.ResetQueue();

        (await Decide(runId, "approve")).EnsureSuccessStatusCode();

        var queued = await Load(runId);
        queued.State.ShouldBe("Queued");
        queued.ApprovedAt.ShouldNotBeNull();
        (await QueuedRunIds()).ShouldBe([runId]);

        // Phase 2, with the approved Plan in the instruction — without that, approval is
        // theatre (design D2).
        fixture.Agent.Result = new AgentResult(
            Succeeded: true,
            Log: "done",
            OutputLink: null,
            Usage: null
        );
        await Execute(runId);

        var finished = await Load(runId);
        finished.State.ShouldBe("Succeeded");
        finished.OutputLink.ShouldBe("https://github.com/acme/portal/pull/1");
        fixture.Agent.Instructions.Last().Prompt.ShouldContain("Change the thing.");
        fixture.Agent.Instructions.Last().Prompt.ShouldContain("A human approved this plan");
    }

    [Fact]
    public async Task Rejection_Should_EndTheRunAndFreeTheStory()
    {
        var runId = await PauseOnPlan();
        await fixture.ResetQueue();

        (await Decide(runId, "reject")).EnsureSuccessStatusCode();

        (await Load(runId)).State.ShouldBe("Cancelled");
        (await QueuedRunIds()).ShouldBeEmpty();

        // Terminal means the Story is free again (BR-001/BR-004: a human re-triggers).
        var second = await Dispatch();
        second.ShouldNotBe(runId);
    }

    [Fact]
    public async Task Waiting_Should_HoldTheStoryButNoCapSlot()
    {
        var runId = await PauseOnPlan();

        // BR-001: the Story is still spoken for.
        var blocked = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "9", automationId = _automationId }
        );
        blocked.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // BR-002: but waiting is not work — the cap counts Planning/Executing only, so another
        // Story dispatches even with this one parked.
        fixture.Vendor.Stories.Add(new VendorStory("10", "Another", "open", [], "Also."));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 2);

        var other = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "10", automationId = _automationId }
        );
        other.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await other.Content.ReadFromJsonAsync<RunNowResponse>())!.WaitingAtCap.ShouldBeFalse();

        (await Load(runId)).State.ShouldBe("AwaitingApproval");
    }

    [Fact]
    public async Task ADecisionOnARunThatIsNotWaiting_Should_BeRefusedDistinctly()
    {
        var runId = await Dispatch();

        // Still Queued — nothing has been proposed, so there is nothing to decide on.
        var response = await Decide(runId, "approve");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("NotAwaitingApproval");

        var unknown = await Decide(Guid.CreateVersion7(), "approve");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(
        Guid Id,
        string VendorStoryId,
        string State,
        bool Dispatched,
        bool WaitingAtCap
    );

    sealed record RunRow(
        string State,
        string? Plan,
        DateTimeOffset? ApprovedAt,
        string? OutputLink,
        DateTimeOffset? EndedAt
    );
}
