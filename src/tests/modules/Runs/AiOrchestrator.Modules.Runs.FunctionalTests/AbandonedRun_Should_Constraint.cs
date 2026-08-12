using System.Net.Http.Json;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Features.Execution;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #140 — BR-005 when the process meant to enforce it is gone. What must hold: an overdue Run ends
/// and frees its Story, a Run inside its deadline is untouched, and an outcome already written is
/// never overwritten.
/// </summary>
[Collection(RunsCollection.Name)]
public class AbandonedRun_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
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

        var automation = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "ai:grill",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "story.md",
                timeoutMinutes = 30,
            }
        );
        automation.EnsureSuccessStatusCode();
        _automationId = (await automation.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>A Run in the state a vanished worker leaves behind, started <paramref name="ago"/>.</summary>
    async Task<Guid> AnExecutingRun(TimeSpan ago, RunState state = RunState.Executing)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();

        var run = Run.Create(
            _projectId,
            "7",
            _automationId,
            RunLocus.Sandbox,
            DateTimeOffset.UtcNow - ago
        );
        if (state == RunState.Planning)
        {
            run.MarkPlanning(DateTimeOffset.UtcNow - ago);
        }
        else
        {
            run.MarkExecuting(DateTimeOffset.UtcNow - ago);
        }

        database.Runs.Add(run);
        await database.SaveChangesAsync();
        return run.Id;
    }

    async Task Sweep()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<RunReaping>().Sweep(default);
    }

    async Task<(string State, string? Failure)> Load(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        var run = await database.Runs.AsNoTracking().SingleAsync(entity => entity.Id == runId);
        return (run.State.ToString(), run.FailureReason);
    }

    [Fact]
    public async Task ARunPastItsDeadline_Should_FailNamingTheAbsentWorker()
    {
        var runId = await AnExecutingRun(TimeSpan.FromMinutes(45));

        await Sweep();

        var (state, failure) = await Load(runId);
        state.ShouldBe("Failed");
        failure.ShouldNotBeNull();

        // The distinction that matters: an agent out of time asks for a bigger budget, a worker
        // that vanished asks somebody to look at the infrastructure (design D2).
        failure.ShouldContain("without its worker reporting");
    }

    [Fact]
    public async Task AReapedRun_Should_FreeItsStory()
    {
        await AnExecutingRun(TimeSpan.FromMinutes(45));
        await Sweep();

        // BR-001's partial unique index is the authority: a second active Run for the same Story
        // is only insertable because the first is terminal.
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        database.Runs.Add(
            Run.Create(_projectId, "7", _automationId, RunLocus.Sandbox, DateTimeOffset.UtcNow)
        );
        await Should.NotThrowAsync(() => database.SaveChangesAsync());
    }

    [Fact]
    public async Task ARunInsideItsDeadline_Should_BeUntouched()
    {
        // Twenty minutes into a thirty-minute budget: quiet, and none of the reaper's business.
        var runId = await AnExecutingRun(TimeSpan.FromMinutes(20));

        await Sweep();

        (await Load(runId)).State.ShouldBe("Executing");
    }

    [Fact]
    public async Task ARunJustPastItsTimeoutButInsideTheGrace_Should_BeUntouched()
    {
        // Past thirty minutes, inside the grace: a worker finishing must be allowed to.
        var runId = await AnExecutingRun(TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(30));

        await Sweep();

        (await Load(runId)).State.ShouldBe("Executing");
    }

    [Fact]
    public async Task APlanningRun_Should_BeReapedToo()
    {
        var runId = await AnExecutingRun(TimeSpan.FromMinutes(45), RunState.Planning);

        await Sweep();

        (await Load(runId)).State.ShouldBe("Failed");
    }

    [Fact]
    public async Task ARunThatFinishedFirst_Should_KeepItsOwnOutcome()
    {
        var runId = await AnExecutingRun(TimeSpan.FromMinutes(45));

        // The race the conditional write exists for: the worker was alive after all and completed
        // between the sweep's read and its write (design D5).
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
            var run = await database.Runs.SingleAsync(entity => entity.Id == runId);
            run.Succeed(DateTimeOffset.UtcNow, null, null, null, null);
            await database.SaveChangesAsync();
        }

        await Sweep();

        var (state, failure) = await Load(runId);
        state.ShouldBe("Succeeded");
        failure.ShouldBeNull();
    }

    /// <summary>
    /// The state an approval-gated Run is in the moment a human says yes: planned long ago, waited,
    /// and only now executing. <paramref name="plannedAgo"/> is deliberately far past the timeout.
    /// </summary>
    async Task<Guid> AnApprovedRun(TimeSpan plannedAgo, TimeSpan executingFor)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();

        var run = Run.Create(
            _projectId,
            "8",
            _automationId,
            RunLocus.Sandbox,
            DateTimeOffset.UtcNow - plannedAgo
        );
        run.MarkPlanning(DateTimeOffset.UtcNow - plannedAgo);
        run.AwaitApproval(DateTimeOffset.UtcNow - plannedAgo, "A plan.");
        run.Approve(DateTimeOffset.UtcNow - executingFor);
        run.MarkExecuting(DateTimeOffset.UtcNow - executingFor);

        database.Runs.Add(run);
        await database.SaveChangesAsync();
        return run.Id;
    }

    [Fact]
    public async Task ARunApprovedAfterALongWait_Should_NotBeReapedForTheWait()
    {
        // Planned two days ago, approved a moment ago: BR-006 says the wait is untimed, so the only
        // clock that counts is the executing phase's, which has barely started (#146).
        var runId = await AnApprovedRun(
            plannedAgo: TimeSpan.FromDays(2),
            executingFor: TimeSpan.FromSeconds(5)
        );

        await Sweep();

        (await Load(runId)).State.ShouldBe("Executing");
    }

    [Fact]
    public async Task AnApprovedRunWhoseExecutionIsOverdue_Should_StillBeReaped()
    {
        // The same shape, but the executing phase itself is past its budget — which is exactly what
        // #140 exists to end, and #146 must not have disabled.
        var runId = await AnApprovedRun(
            plannedAgo: TimeSpan.FromDays(2),
            executingFor: TimeSpan.FromMinutes(45)
        );

        await Sweep();

        (await Load(runId)).State.ShouldBe("Failed");
    }

    [Fact]
    public async Task ATerminalRun_Should_NeverBeConsidered()
    {
        var runId = await AnExecutingRun(TimeSpan.FromMinutes(45));
        await Sweep();
        var first = await Load(runId);

        // Idempotent: a second pass over the same overdue Run changes nothing, so a restarted
        // sweeper cannot rewrite what an earlier pass decided.
        await Sweep();
        (await Load(runId)).ShouldBe(first);
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);
}
