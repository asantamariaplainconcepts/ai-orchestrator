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
/// UC-014. The interesting case is not the button — it is the race: a Run cancelled while its
/// agent is working must stay cancelled and publish nothing, or the human's decision silently
/// loses to a result that arrived afterwards.
/// </summary>
[Collection(RunsCollection.Name)]
public class RunCancellation_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
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
                triggerLabel = "ai:implement",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "story.md",
            }
        );
        automation.EnsureSuccessStatusCode();
        _automationId = (await automation.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;

        fixture.Vendor.Stories.Add(new VendorStory("9", "A story", "open", [], "Do it."));
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

    Task<HttpResponseMessage> Cancel(Guid runId) =>
        _client.PostAsync($"/api/projects/{_projectId}/runs/{runId}/cancel", content: null);

    async Task Execute(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRunExecutor>().Execute(runId);
    }

    async Task<(string State, string? Output, string? Failure)> Load(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        return await database
            .Runs.Where(run => run.Id == runId)
            .Select(run => new ValueTuple<string, string?, string?>(
                run.State.ToString(),
                run.OutputLink,
                run.FailureReason
            ))
            .SingleAsync();
    }

    [Fact]
    public async Task AQueuedRun_Should_BeDiscardedAndFreeItsStory()
    {
        var runId = await Dispatch();

        (await Cancel(runId)).EnsureSuccessStatusCode();

        var (state, _, failure) = await Load(runId);
        state.ShouldBe("Cancelled");
        // A deliberate act is not a fault — nothing invents a reason.
        failure.ShouldBeNull();

        // Terminal frees the Story (BR-001).
        var second = await Dispatch();
        second.ShouldNotBe(runId);
    }

    [Fact]
    public async Task ARunCancelledBeforeExecution_Should_NeverInvokeTheRuntime()
    {
        var runId = await Dispatch();
        (await Cancel(runId)).EnsureSuccessStatusCode();

        await Execute(runId);

        fixture.Agent.Instructions.ShouldBeEmpty();
        (await Load(runId)).State.ShouldBe("Cancelled");
    }

    [Fact]
    public async Task ARunCancelledDuringItsInvocation_Should_PublishNothingAndStayCancelled()
    {
        var runId = await Dispatch();

        // The race made deterministic: the fake runtime cancels the Run from inside the
        // invocation, exactly as a human clicking mid-flight would.
        fixture.Agent.OnExecute = async () => (await Cancel(runId)).EnsureSuccessStatusCode();

        await Execute(runId);

        var (state, output, _) = await Load(runId);
        state.ShouldBe("Cancelled");
        output.ShouldBeNull();
        // The consequence is what cancellation actually prevents.
        fixture.Workspace.Published.ShouldBeFalse();
    }

    [Fact]
    public async Task ATerminalRun_Should_RefuseCancellationNamingItsState()
    {
        var runId = await Dispatch();
        await Execute(runId);
        (await Load(runId)).State.ShouldBe("Succeeded");

        var response = await Cancel(runId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Succeeded");
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);
}
