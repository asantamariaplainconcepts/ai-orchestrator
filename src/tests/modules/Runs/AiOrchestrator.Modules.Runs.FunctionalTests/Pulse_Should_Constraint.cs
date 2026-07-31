using System.Net.Http.Json;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #108 — every pulse figure must be derivable by hand from the run list (design D2). The
/// expected values here are computed with exactly that arithmetic: seeded timestamps, counted
/// states, summed known costs.
/// </summary>
[Collection(RunsCollection.Name)]
public class Pulse_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;
    Guid _refineId;

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

        _refineId = await CreateAutomation("ai:refine", "RepositoryPrompt");

        fixture.Vendor.Stories.Add(new VendorStory("1", "First", "open", [], "Body."));
        fixture.Vendor.Stories.Add(new VendorStory("2", "Second", "open", [], "Body."));
        fixture.Vendor.Stories.Add(new VendorStory("3", "Third", "open", [], "Body."));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 3);
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

    async Task<Guid> CreateAutomation(string trigger, string action)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = trigger,
                triggerState = (string?)null,
                action,
                runtime = "ClaudeCodeHeadless",
                requiresApproval = false,
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;
    }

    /// <summary>Seeds a Run through the domain API so every timestamp is deliberate.</summary>
    async Task Seed(string story, DateTimeOffset created, Action<Run> shape)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        var run = Run.Create(_projectId, story, _refineId, created);
        shape(run);
        database.Runs.Add(run);
        await database.SaveChangesAsync();
    }

    async Task<PulseResponse> Pulse(Guid projectId) =>
        (await _client.GetFromJsonAsync<PulseResponse>($"/api/projects/{projectId}/pulse"))!;

    [Fact]
    public async Task EveryFigure_Should_BeDerivableByHand()
    {
        var now = DateTimeOffset.UtcNow;

        // In window: queue wait 20s, duration 60s, cost 0.50, succeeded.
        var first = now.AddDays(-1);
        await Seed(
            "1",
            first,
            run =>
            {
                run.MarkDispatched(first.AddSeconds(10));
                run.MarkExecuting(first.AddSeconds(30));
                run.Succeed(first.AddSeconds(90), null, 100, 200, 0.50m);
            }
        );

        // In window: queue wait 10s, duration 30s, failed, cost unknown (BR-011).
        var second = now.AddDays(-2);
        await Seed(
            "2",
            second,
            run =>
            {
                run.MarkDispatched(second.AddSeconds(10));
                run.MarkExecuting(second.AddSeconds(20));
                run.Fail(second.AddSeconds(50), "boom");
            }
        );

        // Outside the window: must not move any window figure, but counts for coverage.
        var old = now.AddDays(-8);
        await Seed(
            "9",
            old,
            run =>
            {
                run.MarkDispatched(old.AddSeconds(1));
                run.MarkExecuting(old.AddSeconds(2));
                run.Succeed(old.AddSeconds(3), null, 1, 1, 9.99m);
            }
        );

        var pulse = await Pulse(_projectId);

        pulse.RunsStarted.ShouldBe(2);
        pulse.TerminalRuns.ShouldBe(2);
        pulse.SuccessRate!.Value.ShouldBe(0.5, 0.001);
        pulse.KnownCostUsd.ShouldBe(0.50m);
        pulse.ReportedRuns.ShouldBe(1);
        pulse.UnknownCostRuns.ShouldBe(1);
        pulse.MeanQueueWaitSeconds!.Value.ShouldBe(15.0, 0.5);
        pulse.MeanDurationSeconds!.Value.ShouldBe(45.0, 0.5);

        // The automation fired twice in the window (the old run is outside it), failed once.
        var refine = pulse.Automations.Single(entry => entry.AutomationId == _refineId);
        refine.TriggerLabel.ShouldBe("ai:refine");
        refine.Action.ShouldBe("RepositoryPrompt");
        refine.Fired.ShouldBe(2);
        refine.Failed.ShouldBe(1);

        // Coverage is all-time: stories 1, 2 ran (and 9, gone from the mirror); 3 never did.
        pulse.StoriesTotal.ShouldBe(3);
        pulse.StoriesNeverRun.ShouldBe(1);

        // The failure waits on a human (no newer run for its story) — inbox arithmetic, scoped.
        pulse.Waiting.Failure.ShouldBe(1);
        pulse.Waiting.Approval.ShouldBe(0);
        pulse.Waiting.Input.ShouldBe(0);
    }

    [Fact]
    public async Task AnUnusedAutomation_Should_AppearRatherThanBeOmitted()
    {
        var unused = await CreateAutomation("ai:estimate", "Estimate");

        var pulse = await Pulse(_projectId);

        var entry = pulse.Automations.Single(candidate => candidate.AutomationId == unused);
        entry.Fired.ShouldBe(0);
        entry.Failed.ShouldBe(0);
    }

    [Fact]
    public async Task TheOldestOpenQuestion_Should_CarryItsAge()
    {
        var asked = DateTimeOffset.UtcNow.AddHours(-1);
        await Seed(
            "3",
            asked.AddMinutes(-1),
            run =>
            {
                run.MarkExecuting(asked.AddMinutes(-1));
                run.AwaitInput(asked);
            }
        );

        var pulse = await Pulse(_projectId);

        pulse.Waiting.Input.ShouldBe(1);
        pulse.OldestOpenQuestionSeconds!.Value.ShouldBe(3600, 60);
    }

    [Fact]
    public async Task AnEmptyProject_Should_HaveAPulseOfZeros()
    {
        var empty = await CreateProject();

        var pulse = await Pulse(empty);

        pulse.RunsStarted.ShouldBe(0);
        pulse.TerminalRuns.ShouldBe(0);
        pulse.SuccessRate.ShouldBeNull();
        pulse.KnownCostUsd.ShouldBe(0m);
        pulse.UnknownCostRuns.ShouldBe(0);
        pulse.MeanQueueWaitSeconds.ShouldBeNull();
        pulse.MeanDurationSeconds.ShouldBeNull();
        pulse.Automations.ShouldBeEmpty();
        pulse.StoriesTotal.ShouldBe(0);
        pulse.StoriesNeverRun.ShouldBe(0);
        pulse.Waiting.ShouldBe(new WaitingResponse(0, 0, 0));
        pulse.OldestOpenQuestionSeconds.ShouldBeNull();
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record AutomationEntryResponse(
        Guid AutomationId,
        string TriggerLabel,
        string Action,
        int Fired,
        int Failed
    );

    sealed record WaitingResponse(int Approval, int Input, int Failure);

    sealed record PulseResponse(
        int RunsStarted,
        int TerminalRuns,
        double? SuccessRate,
        decimal KnownCostUsd,
        int ReportedRuns,
        int UnknownCostRuns,
        double? MeanQueueWaitSeconds,
        double? MeanDurationSeconds,
        IReadOnlyList<AutomationEntryResponse> Automations,
        int StoriesTotal,
        int StoriesNeverRun,
        WaitingResponse Waiting,
        double? OldestOpenQuestionSeconds
    );
}
