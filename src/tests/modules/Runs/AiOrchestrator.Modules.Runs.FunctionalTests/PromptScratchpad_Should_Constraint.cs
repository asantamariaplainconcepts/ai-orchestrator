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
/// #189 — an Admin tries a prompt before committing it.
/// <para>
/// A scratchpad is a conversation started fresh per attempt (design D1), so these assertions are
/// about the properties that make it a <i>trial</i> rather than about a new capability: the prompt
/// text reaches the agent, each attempt is uncontaminated by the last, a Story is framed exactly as
/// a Run frames it, and nothing about trying changes what a Run afterwards will do.
/// </para>
/// </summary>
[Collection(RunsCollection.Name)]
public class PromptScratchpad_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;

    const string Draft = "Estimate this story in points and explain the number.";

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Agent.Reset();
        fixture.Conversations.Reset();
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
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// One attempt, exactly as the portal makes it: a fresh conversation, then the prompt text as its
    /// only message. Written as a helper because that pairing <i>is</i> the design — a test that
    /// reused one conversation would be testing something the surface never does.
    /// </summary>
    async Task<Guid> Attempt(string prompt, string? vendorStoryId = null)
    {
        var started = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/conversations",
            new { vendorStoryId }
        );
        started.StatusCode.ShouldBe(
            HttpStatusCode.Created,
            await started.Content.ReadAsStringAsync()
        );
        var conversationId = (await started.Content.ReadFromJsonAsync<ConversationResponse>())!.Id;

        var said = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/conversations/{conversationId}/messages",
            new { body = prompt }
        );
        said.StatusCode.ShouldBe(HttpStatusCode.OK, await said.Content.ReadAsStringAsync());

        return conversationId;
    }

    async Task Story(string id, string title, string state, string[] labels, string body)
    {
        fixture.Vendor.Stories.Add(new VendorStory(id, title, state, labels, body));
        (
            await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null)
        ).EnsureSuccessStatusCode();
        await fixture.Probe.WaitForAtLeast(_projectId, 1);
    }

    [Fact]
    public async Task TheDraft_Should_ReachTheAgentAndCreateNoRun()
    {
        await Attempt(Draft);

        fixture.Conversations.Passes.ShouldBe(1);
        fixture.Conversations.Calls.Single().Message.ShouldBe(Draft);

        // The repository, with the project's credential named and never valued (BR-010).
        var context = fixture.Conversations.Calls.Single().Context;
        context.SecretName.ShouldBe("acme-pat");
        context.Code.Repository.ShouldBe("portal");

        // Counted, not inferred from a missing badge: trying occupies nothing (BR-001, BR-002).
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        (await database.Runs.AnyAsync(run => run.ProjectId == _projectId)).ShouldBeFalse();
    }

    [Fact]
    public async Task AnEditedDraft_Should_BeTriedAfresh()
    {
        // The property that makes a trial a trial. A second attempt inside one conversation would
        // reach an agent holding the first draft and its own reply — and where the habitat keeps a
        // warm container per conversation (DEC-061), the same workspace too.
        await Attempt(Draft);
        await Attempt("Actually: estimate it in hours.");

        var calls = fixture.Conversations.Calls;
        calls.Count.ShouldBe(2);
        calls[0].ConversationId.ShouldNotBe(calls[1].ConversationId);
        calls[1].Message.ShouldBe("Actually: estimate it in hours.");
    }

    [Fact]
    public async Task AStory_Should_BeFramedAsARunFramesIt()
    {
        await Story("7", "A story", "open", ["ai:refine"], "Do the thing.");

        await Attempt(Draft, "7");

        // The whole fidelity argument (design D3): state and labels are exactly what a real prompt
        // branches on, and before #189 a conversation supplied neither.
        var storyContext = fixture.Conversations.Calls.Single().Context.StoryContext;
        storyContext.ShouldNotBeNull();
        storyContext.ShouldContain("Story #7: A story");
        storyContext.ShouldContain("State: open; labels: ai:refine.");
        storyContext.ShouldContain("Do the thing.");

        // Identical, not merely similar — asserted by running the other path over the same Story and
        // comparing what each handed the agent. A shared helper both callers ignore in different
        // places would pass a test written against the helper alone.
        fixture.Vendor.Documents["ai/prompts/estimate.md"] = "The committed prompt.";
        var automation = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "ai:estimate",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "estimate.md",
                requiresApproval = false,
            }
        );
        automation.EnsureSuccessStatusCode();
        var automationId = (await automation.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;

        var dispatched = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "7", automationId }
        );
        dispatched.StatusCode.ShouldBe(
            HttpStatusCode.Created,
            await dispatched.Content.ReadAsStringAsync()
        );
        var runId = (await dispatched.Content.ReadFromJsonAsync<RunNowResponse>())!.Id;

        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRunExecutor>().Execute(runId);

        fixture.Agent.Instructions.Single().Prompt.ShouldContain(storyContext);
    }

    [Fact]
    public async Task ARunAfterwards_Should_ResolveItsPromptFromTheRepository()
    {
        await Story("8", "A story", "open", [], "Do the thing.");
        fixture.Vendor.Documents["ai/prompts/estimate.md"] = "The committed prompt.";

        await Attempt("A draft that must never be run by anything.", "8");

        var automation = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "ai:estimate",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "estimate.md",
                requiresApproval = false,
            }
        );
        automation.EnsureSuccessStatusCode();
        var automationId = (await automation.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;

        var dispatched = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "8", automationId }
        );
        dispatched.StatusCode.ShouldBe(
            HttpStatusCode.Created,
            await dispatched.Content.ReadAsStringAsync()
        );
        var runId = (await dispatched.Content.ReadFromJsonAsync<RunNowResponse>())!.Id;

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRunExecutor>().Execute(runId);
        }

        // Asserted on the instruction the agent actually received, which is the only place a second
        // source for a prompt could ever show up.
        var instruction = fixture.Agent.Instructions.Single().Prompt;
        instruction.ShouldContain("The committed prompt.");
        instruction.ShouldNotContain("A draft that must never be run by anything.");
    }

    [Fact]
    public async Task AnAttemptInFlight_Should_NotStopTheStorysAutomations()
    {
        await Story("9", "A busy story", "open", [], "Do the thing.");

        await Attempt(Draft, "9");

        var automation = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "ai:refine",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "story.md",
                requiresApproval = false,
            }
        );
        automation.EnsureSuccessStatusCode();
        var automationId = (await automation.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;

        var run = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "9", automationId }
        );

        run.StatusCode.ShouldBe(HttpStatusCode.Created, await run.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AFailedAttempt_Should_LeaveTheScratchpadUsable()
    {
        fixture.Conversations.Next = new ConversationReply(false, "The agent timed out.", null);

        var first = await Attempt(Draft);

        var failed = await _client.GetFromJsonAsync<ConversationDetail>(
            $"/api/projects/{_projectId}/conversations/{first}"
        );
        failed!.Messages.Single(message => message.Role == "Agent").Failed.ShouldBeTrue();

        // Unmeasured is unknown, not free (BR-011) — on a draft as on anything else, because the
        // pass was paid for either way.
        failed.Messages.Single(message => message.Role == "Agent").CostUsd.ShouldBeNull();
        failed.SpendIsComplete.ShouldBeFalse();

        // And the next attempt works, which is what "stays usable" means.
        fixture.Conversations.Next = new ConversationReply(
            true,
            "8 points.",
            new AgentUsage(1, 1, 0.01m)
        );
        await Attempt(Draft);
        fixture.Conversations.Passes.ShouldBe(2);
    }

    [Fact]
    public async Task ARealPrompt_Should_NotBeRefusedForItsLength()
    {
        // The measurement that set the bound (design D6): the largest real prompt observed is 9,741
        // characters against an old cap of 10,000.
        //
        // The realistic length alone is NOT enough to pin the decision — 9,741 was accepted by the
        // old cap too, so a test asserting only that would have stayed green while the bound was
        // reverted, which is exactly what the mutation check caught. Both edges of the new bound are
        // asserted instead, and the realistic case stays because it is why the bound moved.
        var started = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/conversations",
            new { vendorStoryId = (string?)null }
        );
        var conversationId = (await started.Content.ReadFromJsonAsync<ConversationResponse>())!.Id;

        foreach (var length in new[] { 9_741, 40_000 })
        {
            var accepted = await _client.PostAsJsonAsync(
                $"/api/projects/{_projectId}/conversations/{conversationId}/messages",
                new { body = new string('x', length) }
            );
            accepted.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                $"{length} characters: {await accepted.Content.ReadAsStringAsync()}"
            );
        }

        var refused = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/conversations/{conversationId}/messages",
            new { body = new string('x', 40_001) }
        );
        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record ConversationResponse(Guid Id);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);

    sealed record ConversationDetail(
        Guid Id,
        decimal SpendUsd,
        bool SpendIsComplete,
        IReadOnlyList<MessageDetail> Messages
    );

    sealed record MessageDetail(string Role, string Body, bool Failed, decimal? CostUsd);
}
