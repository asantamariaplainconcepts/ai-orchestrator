using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// UC-006's removal half (#84). Tested here rather than beside the other Automation endpoints
/// because the rule under test is about <i>Runs</i> — "delete what was never used" is only
/// meaningful where Runs can actually exist.
/// </summary>
[Collection(RunsCollection.Name)]
public class DeleteAutomation_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
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

        fixture.Vendor.Stories.Add(new VendorStory("9", "A story", "open", [], "Do it."));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 1);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task<Guid> CreateAutomation(string trigger, Guid? projectId = null)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId ?? _projectId}/automations",
            new
            {
                triggerLabel = trigger,
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                requiresApproval = false,
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;
    }

    Task<HttpResponseMessage> Delete(Guid automationId, Guid? projectId = null) =>
        _client.DeleteAsync($"/api/projects/{projectId ?? _projectId}/automations/{automationId}");

    async Task<int> AutomationCount() =>
        (
            await _client.GetFromJsonAsync<IReadOnlyList<AutomationResponse>>(
                $"/api/projects/{_projectId}/automations"
            )
        )!.Count;

    async Task<Guid> RunWith(Guid automationId)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "9", automationId }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RunNowResponse>())!.Id;
    }

    [Fact]
    public async Task AnUnusedAutomation_Should_BeGone()
    {
        var id = await CreateAutomation("ai:unused");

        (await Delete(id)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await AutomationCount()).ShouldBe(0);
    }

    [Fact]
    public async Task AUsedAutomation_Should_BeRefusedWithItsRunCount()
    {
        var id = await CreateAutomation("ai:used");
        await RunWith(id);

        var response = await Delete(id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("1 run references it");
        // The message teaches the rule rather than only enforcing it.
        body.ShouldContain("Disable it instead");
        (await AutomationCount()).ShouldBe(1);
    }

    [Fact]
    public async Task ARefusedDeletion_Should_LeaveTheInFlightRunAbleToFinish()
    {
        var id = await CreateAutomation("ai:inflight");
        var runId = await RunWith(id);

        (await Delete(id)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // The point of the refusal: the executor still resolves the Automation mid-Run. A hard
        // delete here is exactly the failure #14 removed, in a form nobody could undo.
        fixture.Agent.Result = new AgentResult(
            Succeeded: true,
            Log: "A comment.",
            OutputLink: null,
            Usage: null
        );
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRunExecutor>().Execute(runId);

        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        var state = await database
            .Runs.Where(run => run.Id == runId)
            .Select(run => run.State.ToString())
            .SingleAsync();
        state.ShouldBe("Succeeded");
    }

    [Fact]
    public async Task ADeletedTrigger_Should_BeAvailableAgain()
    {
        var id = await CreateAutomation("ai:recycled");
        (await Delete(id)).EnsureSuccessStatusCode();

        // BR-003 queries live rows, so the freed label is simply free — worth asserting because
        // recreating what you just deleted is the first thing anyone does after a mistake.
        var again = await CreateAutomation("ai:recycled");

        again.ShouldNotBe(id);
        (await AutomationCount()).ShouldBe(1);
    }

    [Fact]
    public async Task AnotherProjectsAutomation_Should_BeNotFound()
    {
        var other = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"other-{Guid.NewGuid():N}" }
        );
        other.EnsureSuccessStatusCode();
        var otherId = (await other.Content.ReadFromJsonAsync<ProjectResponse>())!.Id;
        var foreign = await CreateAutomation("ai:elsewhere", otherId);

        var response = await Delete(foreign);

        // Not found rather than forbidden: an error must not reveal what exists elsewhere.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);
}
