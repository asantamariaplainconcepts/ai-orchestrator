using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Runs.Features.Conversation;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// UC-024 — the grill. What must hold: the rubric is read before anything is written; questions
/// wait rather than conclude; READY becomes a chainable label plus a verdict; and the whole
/// conversation reaches the next pass.
/// </summary>
[Collection(RunsCollection.Name)]
public class GrillAction_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;
    Guid _automationId;

    const string Rubric = "Every story needs: acceptance criteria, an actor, an out-of-scope.";

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
                triggerLabel = "ai:grill",
                triggerState = (string?)null,
                action = "GrillToReady",
                runtime = "ClaudeCodeHeadless",
                requiresApproval = false,
            }
        );
        automation.EnsureSuccessStatusCode();
        _automationId = (await automation.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;

        fixture.Vendor.Documents["docs/process/definition-of-ready.md"] = Rubric;
        fixture.Vendor.Stories.Add(new VendorStory("9", "A story", "open", [], "Vague wish."));
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

    async Task<(string State, string? Failure)> Load(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        return await database
            .Runs.Where(run => run.Id == runId)
            .Select(run => new ValueTuple<string, string?>(run.State.ToString(), run.FailureReason))
            .SingleAsync();
    }

    void AgentSays(string reply) =>
        fixture.Agent.Result = new AgentResult(
            Succeeded: true,
            Log: reply,
            OutputLink: null,
            Usage: null
        );

    [Fact]
    public async Task Gaps_Should_BecomeQuestionsAndAWait()
    {
        AgentSays("Which actor is this for? What is out of scope?");
        var runId = await Dispatch();

        await Execute(runId);

        (await Load(runId)).State.ShouldBe("AwaitingInput");
        var posted = fixture.Vendor.Comments.Single();
        posted.ShouldContain("Which actor is this for?");
        posted.ShouldContain($"aio:run:{runId:D}");

        // The rubric reached the agent — the instruction carried the project's own bar.
        fixture.Agent.Instructions.Single().Prompt.ShouldContain("acceptance criteria");
    }

    [Fact]
    public async Task AnAnsweredGrill_Should_MarkReadyWithTheConversationInHand()
    {
        AgentSays("What is out of scope?");
        var runId = await Dispatch();
        await Execute(runId);

        fixture.Vendor.StoryComments.Add(
            ("9", new StoryComment("Out of scope: billing.", DateTimeOffset.UtcNow.AddSeconds(5)))
        );
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            await ResumeChecker.CheckOnce(scope.ServiceProvider, CancellationToken.None);
        }
        (await Load(runId)).State.ShouldBe("Queued");

        AgentSays("READY\nAll three criteria are met.");
        await Execute(runId);

        var (state, _) = await Load(runId);
        state.ShouldBe("Succeeded");

        // The ready label rides the ordinary write path — chainable at the vendor (D4).
        fixture
            .Vendor.Stories.Single(story => story.VendorId == "9")
            .Labels.ShouldContain("ready-for-proposal");
        fixture.Vendor.Comments.Last().ShouldContain("All three criteria are met.");

        // The second pass saw the human's answer: stateless, rebuilt from the vendor (D3).
        fixture.Agent.Instructions.Last().Prompt.ShouldContain("Out of scope: billing.");
    }

    [Fact]
    public async Task AnAlreadyReadyStory_Should_PassOnTheFirstAsk()
    {
        AgentSays("READY\nEverything demanded is present.");
        var runId = await Dispatch();

        await Execute(runId);

        (await Load(runId)).State.ShouldBe("Succeeded");
        fixture
            .Vendor.Stories.Single(story => story.VendorId == "9")
            .Labels.ShouldContain("ready-for-proposal");
    }

    [Fact]
    public async Task AMissingRubric_Should_FailBeforeTouchingTheStory()
    {
        fixture.Vendor.Documents.Clear();
        AgentSays("READY");
        var runId = await Dispatch();

        await Execute(runId);

        var (state, failure) = await Load(runId);
        state.ShouldBe("Failed");
        failure!.ShouldContain("docs/process/definition-of-ready.md");

        // Fail-before-write (D2): no comment, no label, no agent spend.
        fixture.Vendor.Comments.ShouldBeEmpty();
        fixture.Vendor.Stories.Single(story => story.VendorId == "9").Labels.ShouldBeEmpty();
        fixture.Agent.Instructions.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheReadyLabel_Should_TriggerTheNextAutomationThroughOrdinaryMatching()
    {
        // The chain: grill's ready label is the next Automation's trigger. No orchestration
        // code exists — this test proves reconciliation and matching carry the baton.
        var next = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "ready-for-proposal",
                triggerState = (string?)null,
                action = "RefineOrComment",
                runtime = "ClaudeCodeHeadless",
                requiresApproval = false,
            }
        );
        next.EnsureSuccessStatusCode();

        AgentSays("READY\nMet.");
        var runId = await Dispatch();
        await Execute(runId);
        (await Load(runId)).State.ShouldBe("Succeeded");

        // The label is at the vendor; the poll mirrors it and matching fires.
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);

        // Matching rides CAP's background dispatch — the matched Run lands shortly after the
        // refresh returns, not within it. Same deadline-poll shape as DeliveryProbe.
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        var runs = await database.Runs.Where(run => run.ProjectId == _projectId).ToListAsync();

        while (DateTime.UtcNow < deadline && runs.Count < 2)
        {
            await Task.Delay(100);
            runs = await database.Runs.Where(run => run.ProjectId == _projectId).ToListAsync();
        }

        runs.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ACustomRubricAndLabel_Should_BeUsedExactly()
    {
        var custom = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "ai:grill-custom",
                triggerState = (string?)null,
                action = "GrillToReady",
                runtime = "ClaudeCodeHeadless",
                requiresApproval = false,
                rubricPath = "docs/our-own-bar.md",
                readyLabel = "vetted",
            }
        );
        custom.EnsureSuccessStatusCode();
        var customId = (await custom.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;
        fixture.Vendor.Documents["docs/our-own-bar.md"] = "One rule: has a title.";

        AgentSays("READY\nIt has a title.");
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "9", automationId = customId }
        );
        response.EnsureSuccessStatusCode();
        var runId = (await response.Content.ReadFromJsonAsync<RunNowResponse>())!.Id;
        await Execute(runId);

        (await Load(runId)).State.ShouldBe("Succeeded");
        fixture
            .Vendor.Stories.Single(story => story.VendorId == "9")
            .Labels.ShouldContain("vetted");
        fixture.Agent.Instructions.Single().Prompt.ShouldContain("One rule: has a title.");
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);
}
