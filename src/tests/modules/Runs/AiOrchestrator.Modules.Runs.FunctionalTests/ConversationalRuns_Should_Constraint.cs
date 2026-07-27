using System.Net.Http.Json;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Features.Conversation;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #78 — the waiting machinery, exercised without any action consuming it (that is #79). The
/// properties that matter: the marker, not the author, decides what counts as an answer; waiting
/// blocks the Story exactly as executing does; and cancellation wins over any later comment.
/// </summary>
[Collection(RunsCollection.Name)]
public class ConversationalRuns_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
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
                action = "ImplementToPullRequest",
                runtime = "ClaudeCodeHeadless",
                requiresApproval = false,
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

    /// <summary>Drives the primitive the way #79's action will, without existing yet.</summary>
    async Task AskAndWait(Guid runId, string questions)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        var gate = scope.ServiceProvider.GetRequiredService<ConversationGate>();

        var run = await database.Runs.SingleAsync(entity => entity.Id == runId);
        (await gate.AskAndWait(run, questions, DateTimeOffset.UtcNow)).ShouldBeNull();
        await database.SaveChangesAsync();
    }

    async Task CheckOnce()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await ResumeChecker.CheckOnce(scope.ServiceProvider, CancellationToken.None);
    }

    async Task<(string State, DateTimeOffset? WaitingSince)> Load(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        return await database
            .Runs.Where(run => run.Id == runId)
            .Select(run => new ValueTuple<string, DateTimeOffset?>(
                run.State.ToString(),
                run.WaitingSince
            ))
            .SingleAsync();
    }

    void HumanReplies(string body, int inSeconds = 5) =>
        fixture.Vendor.StoryComments.Add(
            ("9", new StoryComment(body, DateTimeOffset.UtcNow.AddSeconds(inSeconds)))
        );

    [Fact]
    public async Task APassEndingWithQuestions_Should_WaitWithTheQuestionsOnTheStory()
    {
        var runId = await Dispatch();

        await AskAndWait(runId, "Which database?");

        var (state, waitingSince) = await Load(runId);
        state.ShouldBe("AwaitingInput");
        waitingSince.ShouldNotBeNull();

        // The questions are on the Story, signed: a reader sees a conversation, the machinery
        // sees its marker.
        var posted = fixture.Vendor.Comments.Single();
        posted.ShouldContain("Which database?");
        posted.ShouldContain($"aio:run:{runId:D}");
    }

    [Fact]
    public async Task AnAnswer_Should_ResumeThroughOrdinaryDispatch()
    {
        var runId = await Dispatch();
        await AskAndWait(runId, "Which database?");

        HumanReplies("Postgres, like everything else here.");
        await CheckOnce();

        var (state, waitingSince) = await Load(runId);
        state.ShouldBe("Queued");
        waitingSince.ShouldBeNull();
    }

    [Fact]
    public async Task TheAgentsOwnComment_Should_NeverResumeIt()
    {
        var runId = await Dispatch();
        await AskAndWait(runId, "Which database?");

        // The dangerous case: the agent's comment lands in the vendor as any other, and one
        // project PAT means its author can be identical to the human's (DEC-030).
        fixture.Vendor.StoryComments.Add(
            (
                "9",
                new StoryComment(
                    RunMarker.Sign(runId, "Which database?"),
                    DateTimeOffset.UtcNow.AddSeconds(5)
                )
            )
        );
        await CheckOnce();

        (await Load(runId)).State.ShouldBe("AwaitingInput");
    }

    [Fact]
    public async Task AWaitingRun_Should_BlockItsStoryLikeAnyActiveRun()
    {
        var runId = await Dispatch();
        await AskAndWait(runId, "Which database?");

        var second = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "9", automationId = _automationId }
        );

        // BR-001: mid-conversation is mid-work.
        second.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    public async Task ACancelledWait_Should_StayCancelledWhenTheAnswerArrivesLate()
    {
        var runId = await Dispatch();
        await AskAndWait(runId, "Which database?");

        (
            await _client.PostAsync(
                $"/api/projects/{_projectId}/runs/{runId}/cancel",
                content: null
            )
        ).EnsureSuccessStatusCode();

        HumanReplies("Postgres.");
        await CheckOnce();

        // The human's cancellation wins over their own answer: terminal is terminal (BR-012),
        // and the Story is already free for a fresh Run that will re-read everything anyway.
        (await Load(runId)).State.ShouldBe("Cancelled");
    }

    [Fact]
    public async Task ACommentOnAStoryWithNoWaitingRun_Should_DispatchNothing()
    {
        HumanReplies("Just chatting.");

        await CheckOnce();

        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        (await database.Runs.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task AVendorOutage_Should_DelayTheResumeNotFailTheRun()
    {
        var runId = await Dispatch();
        await AskAndWait(runId, "Which database?");
        HumanReplies("Postgres.");

        fixture.Vendor.ReadCommentsError =
            AiOrchestrator.Modules.Backlog.Domain.BacklogErrors.VendorUnavailable("down");
        await CheckOnce();
        (await Load(runId)).State.ShouldBe("AwaitingInput");

        // The next tick, with the vendor back, resumes as if nothing happened.
        fixture.Vendor.ReadCommentsError = null;
        await CheckOnce();
        (await Load(runId)).State.ShouldBe("Queued");
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);
}
