using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Dispatch;
using AiOrchestrator.BuildingBlocks.IntegrationEvents;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Features.Matching;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// UC-011 against real containers: a labelled Story becomes a Run and a dispatch message, with
/// BR-001, BR-002 and BR-007 holding. Negatives use the delivery-probe fence — a recorded
/// delivery means the Runs handler already finished, so "no Run exists" is a fact, not a race.
/// </summary>
[Collection(RunsCollection.Name)]
public class RunMatching_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
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

    async Task AddAutomation(string label, string? state = null, bool requiresApproval = false) =>
        (
            await _client.PostAsJsonAsync(
                $"/api/projects/{_projectId}/automations",
                new
                {
                    triggerLabel = label,
                    triggerState = state,
                    action = "RepositoryPrompt",
                    runtime = "ClaudeCodeHeadless",
                    requiresApproval,
                }
            )
        ).EnsureSuccessStatusCode();

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

    [Fact]
    public async Task TheLoop_Should_Close()
    {
        await AddAutomation("run-me");
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", ["run-me"]));

        await Refresh();
        await fixture.Probe.WaitForAtLeast(_projectId, 1);

        var runs = await Runs();
        runs.Count.ShouldBe(1);
        runs[0].VendorStoryId.ShouldBe("1");
        runs[0].State.ShouldBe(nameof(RunState.Queued));
        runs[0].DispatchedAt.ShouldNotBeNull();

        (await QueuedRunIds()).ShouldBe([runs[0].Id]);
    }

    [Fact]
    public async Task ANonMatchingEvent_Should_CreateNothing()
    {
        await AddAutomation("other-label");
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", ["run-me"]));

        await Refresh();
        await fixture.Probe.WaitForAtLeast(_projectId, 1);

        (await Runs()).ShouldBeEmpty();
        (await QueuedRunIds()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ATwoPhaseMatch_Should_CreateAndDispatchLikeAnyOther()
    {
        // Until #22 this asserted a refusal. The lane splits at execution, not creation
        // (approval-gate D1): the Run is created and dispatched, and the worker pauses it on
        // its Plan — which ApprovalGate_Should_Constraint covers.
        await AddAutomation("run-me", requiresApproval: true);
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", ["run-me"]));

        await Refresh();
        await fixture.Probe.WaitForAtLeast(_projectId, 1);

        var run = (await Runs()).Single();
        run.State.ShouldBe(nameof(RunState.Queued));
        (await QueuedRunIds()).ShouldBe([run.Id]);
    }

    [Fact]
    public async Task AStateConstrainedTrigger_Should_MatchOnlyItsState()
    {
        await AddAutomation("run-me", state: "closed");
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", ["run-me"]));

        await Refresh();
        await fixture.Probe.WaitForAtLeast(_projectId, 1);

        (await Runs()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ASecondMatchWhileARunIsActive_Should_BeIgnoredNotQueued()
    {
        await AddAutomation("run-me");
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", ["run-me"]));
        await Refresh();
        await fixture.Probe.WaitForAtLeast(_projectId, 1);

        // The Story changes again while its Run is still active — BR-001 says ignored.
        fixture.Vendor.Stories[0] = new VendorStory("1", "Renamed", "open", ["run-me"]);
        await Refresh();
        await fixture.Probe.WaitForAtLeast(_projectId, 2);

        (await Runs()).Count.ShouldBe(1);
        (await QueuedRunIds()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task ADuplicateDelivery_Should_ChangeNothing()
    {
        await AddAutomation("run-me");
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", ["run-me"]));
        await Refresh();
        await fixture.Probe.WaitForAtLeast(_projectId, 1);

        // At-least-once made concrete: the same event again, straight through the real
        // publisher and relay.
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var events = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
            await events.Publish(new StoryChanged(_projectId, "1", StoryChangeKind.Added));
        }

        await fixture.Probe.WaitForAtLeast(_projectId, 2);

        (await Runs()).Count.ShouldBe(1);
        (await QueuedRunIds()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task AMatchAtTheProjectCap_Should_WaitQueuedAndEnqueueNothing()
    {
        await AddAutomation("run-me");

        // BR-002 counts Planning/Executing, states nothing can reach in this slice — seeded
        // directly, as design D5 says, so the rule is exercised rather than commented.
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
            await database.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO runs.runs ("Id", "ProjectId", "VendorStoryId", "AutomationId", "State", "CreatedAt")
                VALUES
                  ({Guid.CreateVersion7()}, {_projectId}, {"busy-1"}, {Guid.CreateVersion7()}, 'Planning', now()),
                  ({Guid.CreateVersion7()}, {_projectId}, {"busy-2"}, {Guid.CreateVersion7()}, 'Executing', now())
                """
            );
        }

        fixture.Vendor.Stories.Add(new VendorStory("3", "Overflow", "open", ["run-me"]));
        await Refresh();
        await fixture.Probe.WaitForAtLeast(_projectId, 1);

        var overflow = (await Runs()).Single(run => run.VendorStoryId == "3");
        overflow.State.ShouldBe(nameof(RunState.Queued));
        overflow.DispatchedAt.ShouldBeNull();
        (await QueuedRunIds()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ConcurrentDeliveries_Should_CreateExactlyOneRun()
    {
        await AddAutomation("run-me");
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", ["run-me"]));
        await Refresh();
        await fixture.Probe.WaitForAtLeast(_projectId, 1);

        // Clear the naturally created Run so the race starts from zero — the case where the
        // BR-001 pre-check passes for every contender and only the index can decide.
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
            await database.Runs.Where(run => run.ProjectId == _projectId).ExecuteDeleteAsync();
        }
        await fixture.ResetQueue();

        var @event = new StoryChanged(_projectId, "1", StoryChangeKind.Updated);
        await Task.WhenAll(
            Enumerable
                .Range(0, 8)
                .Select(async _ =>
                {
                    await using var scope = fixture.Services.CreateAsyncScope();
                    var handler = scope
                        .ServiceProvider.GetServices<IIntegrationEventHandler<StoryChanged>>()
                        .OfType<StoryChangedHandler>()
                        .Single();
                    // Must not throw: the losers of the insert race report success (BR-001).
                    await handler.Handle(@event, CancellationToken.None);
                })
        );

        (await Runs()).Count.ShouldBe(1);
        (await QueuedRunIds()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task ApplyingATriggerLabelFromThePortal_Should_CreateARun()
    {
        // The whole point of #20 + #24 together: no vendor UI, no terminal — label in the
        // portal, watch the Run exist. UC-008's equivalence (DEC-027) exercised end to end.
        await AddAutomation("ai:implement");
        fixture.Vendor.Stories.Add(new VendorStory("7", "Portal-driven", "open", []));
        await Refresh();
        await fixture.Probe.WaitForAtLeast(_projectId, 1);
        (await Runs()).ShouldBeEmpty();

        (
            await _client.PutAsync(
                $"/api/projects/{_projectId}/backlog/stories/7/labels/ai:implement",
                content: null
            )
        ).EnsureSuccessStatusCode();

        await fixture.Probe.WaitForAtLeast(_projectId, 2);

        var run = (await Runs()).Single();
        run.VendorStoryId.ShouldBe("7");
        (await QueuedRunIds()).ShouldBe([run.Id]);
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record RunRow(
        Guid Id,
        string VendorStoryId,
        Guid AutomationId,
        string State,
        DateTimeOffset? DispatchedAt
    );
}
