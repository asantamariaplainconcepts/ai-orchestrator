using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Dispatch;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// UC-012: detection is the only thing bypassed. Every rule answers the human instead of the
/// handler's silences — that difference in voice is the thing under test.
/// </summary>
[Collection(RunsCollection.Name)]
public class RunNow_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;
    Guid _automationId;

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

        // A mirrored Story WITHOUT the trigger label — the bypass case is the default here.
        fixture.Vendor.Stories.Add(new VendorStory("9", "Unlabelled", "open", []));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 1);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    Task<HttpResponseMessage> Trigger(string vendorStoryId, Guid? automationId = null) =>
        _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId, automationId = automationId ?? _automationId }
        );

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
    public async Task RunNow_Should_DispatchWithoutTheTriggerLabel()
    {
        var response = await Trigger("9");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var run = (await response.Content.ReadFromJsonAsync<RunNowResponse>())!;
        run.Dispatched.ShouldBeTrue();
        run.WaitingAtCap.ShouldBeFalse();

        (await QueuedRunIds()).ShouldBe([run.Id]);
    }

    [Fact]
    public async Task RunNow_Should_AnswerWithAConflictWhenARunIsActive()
    {
        (await Trigger("9")).EnsureSuccessStatusCode();

        var second = await Trigger("9");

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await second.Content.ReadAsStringAsync()).ShouldContain("StoryHasActiveRun");
        (await QueuedRunIds()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task RunNow_Should_WaitQueuedAtTheCapAndSaySo()
    {
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

        var response = await Trigger("9");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var run = (await response.Content.ReadFromJsonAsync<RunNowResponse>())!;
        run.WaitingAtCap.ShouldBeTrue();
        run.Dispatched.ShouldBeFalse();
        (await QueuedRunIds()).ShouldBeEmpty();
    }

    [Fact]
    public async Task RunNow_Should_CreateAnApprovalGatedRunLikeAnyOther()
    {
        // The lane splits at execution, not creation (approval-gate D1) — until #22 this
        // returned a "not implemented yet" refusal.
        var twoPhase = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "ai:review",
                triggerState = (string?)null,
                action = "ImplementToPullRequest",
                runtime = "ClaudeCodeHeadless",
                requiresApproval = true,
            }
        );
        twoPhase.EnsureSuccessStatusCode();
        var twoPhaseId = (await twoPhase.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;

        var response = await Trigger("9", twoPhaseId);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var run = (await response.Content.ReadFromJsonAsync<RunNowResponse>())!;
        run.Dispatched.ShouldBeTrue();
        (await QueuedRunIds()).ShouldBe([run.Id]);
    }

    [Fact]
    public async Task RunNow_Should_RefuseAnUnknownStoryAndAnUnknownAutomation()
    {
        (await Trigger("no-such-story")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var unknownAutomation = await Trigger("9", Guid.CreateVersion7());
        ((int)unknownAutomation.StatusCode).ShouldBe(400);
        (await unknownAutomation.Content.ReadAsStringAsync()).ShouldContain(
            "AutomationNotAvailable"
        );

        (await QueuedRunIds()).ShouldBeEmpty();
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
}
