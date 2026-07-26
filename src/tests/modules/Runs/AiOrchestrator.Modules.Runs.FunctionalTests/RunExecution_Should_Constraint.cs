using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// The agent-execution spec against real containers, the runtime faked at the seam: a claimed
/// Run reaches a terminal state, usage lands or reads unknown (BR-011), a terminal Run frees
/// its Story (BR-001), and nothing secret crosses as a name.
/// </summary>
[Collection(RunsCollection.Name)]
public class RunExecution_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;
    Guid _automationId;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Agent.Reset();
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
                action = "ImplementToPullRequest",
                runtime = "ClaudeCodeHeadless",
                requiresApproval = false,
            }
        );
        automation.EnsureSuccessStatusCode();
        _automationId = (await automation.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;

        fixture.Vendor.Stories.Add(new VendorStory("9", "A story", "open", []));
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
                run.StartedAt,
                run.EndedAt,
                run.FailureReason,
                run.UsageInputTokens,
                run.UsageOutputTokens,
                run.CostUsd
            ))
            .SingleAsync();
    }

    [Fact]
    public async Task AClaimedRun_Should_ReachSucceededWithUsageAndTimestamps()
    {
        var runId = await Dispatch();

        await Execute(runId);

        var run = await Load(runId);
        run.State.ShouldBe("Succeeded");
        run.StartedAt.ShouldNotBeNull();
        run.EndedAt.ShouldNotBeNull();
        run.UsageInputTokens.ShouldBe(10);
        run.UsageOutputTokens.ShouldBe(20);
        run.CostUsd.ShouldBe(0.05m);

        // What crossed the seam: values resolved in-process, and the prompt names the story.
        var instruction = fixture.Agent.Instructions.Single();
        instruction.Credentials.VendorAccessToken.ShouldBe("stub-token");
        instruction.Prompt.ShouldContain("#9");
        instruction.Prompt.ShouldContain("acme/portal");
    }

    [Fact]
    public async Task AbsentUsage_Should_ReadUnknownAndNeverFail()
    {
        fixture.Agent.Result = new AgentResult(
            Succeeded: true,
            Log: "done",
            OutputLink: null,
            Usage: null
        );
        var runId = await Dispatch();

        await Execute(runId);

        var run = await Load(runId);
        run.State.ShouldBe("Succeeded");
        run.UsageInputTokens.ShouldBeNull();
        run.UsageOutputTokens.ShouldBeNull();
        run.CostUsd.ShouldBeNull();
    }

    [Fact]
    public async Task AFailedResult_Should_EndTheRunFailedWithItsReason()
    {
        fixture.Agent.Result = new AgentResult(
            Succeeded: false,
            Log: "the agent gave up",
            OutputLink: null,
            Usage: null
        );
        var runId = await Dispatch();

        await Execute(runId);

        var run = await Load(runId);
        run.State.ShouldBe("Failed");
        run.FailureReason.ShouldBe("the agent gave up");
        run.EndedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task ACrashingRuntime_Should_StillEndTheRun()
    {
        // BR-004: nothing redelivers, so an eternal Executing would hold the Story hostage.
        fixture.Agent.Throws = new InvalidOperationException("container evicted");
        var runId = await Dispatch();

        await Execute(runId);

        (await Load(runId)).State.ShouldBe("Failed");
    }

    [Fact]
    public async Task ATerminalRun_Should_FreeItsStory()
    {
        var first = await Dispatch();
        await Execute(first);
        (await Load(first)).State.ShouldBe("Succeeded");

        // BR-001 constrains active Runs only: the Story runs again.
        var second = await Dispatch();
        second.ShouldNotBe(first);
    }

    [Fact]
    public async Task AMissingRun_Should_BeALoggedNoOp()
    {
        // Must not throw — the message is already deleted (BR-004).
        await Execute(Guid.CreateVersion7());
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);

    sealed record RunRow(
        string State,
        DateTimeOffset? StartedAt,
        DateTimeOffset? EndedAt,
        string? FailureReason,
        long? UsageInputTokens,
        long? UsageOutputTokens,
        decimal? CostUsd
    );
}
