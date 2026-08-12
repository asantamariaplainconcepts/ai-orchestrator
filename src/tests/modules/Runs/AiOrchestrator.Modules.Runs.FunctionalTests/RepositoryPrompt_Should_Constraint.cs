using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #150 — an Automation runs a prompt the project wrote. What must hold: the body reaches the agent
/// and the frontmatter never does, the answer becomes one comment and nothing else, the project's
/// directory decides where a name resolves, and every refusal lands before the spend naming the
/// resolved path.
/// </summary>
[Collection(RunsCollection.Name)]
public class RepositoryPrompt_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;
    Guid _automationId;

    const string Body = "Estimate this story in points and explain the number.";

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

        await Connector(promptDirectory: null);
        _automationId = await Automation("ai:prompt", "estimate.md");

        fixture.Vendor.Stories.Add(new VendorStory("3", "A story", "open", [], "Body."));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 1);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task Connector(string? promptDirectory) =>
        (
            await _client.PutAsJsonAsync(
                $"/api/projects/{_projectId}/connector",
                new
                {
                    owner = "acme",
                    repository = "portal",
                    secretName = "acme-pat",
                    promptDirectory,
                }
            )
        ).EnsureSuccessStatusCode();

    async Task<Guid> Automation(string trigger, string? promptName)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = trigger,
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = promptName,
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;
    }

    async Task<Guid> Dispatch(Guid automationId)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "3", automationId }
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
        var run = await database.Runs.SingleAsync(candidate => candidate.Id == runId);
        return (run.State.ToString(), run.FailureReason);
    }

    [Fact]
    public async Task TheRepositorysPrompt_Should_ReachTheAgentWithTheWorkspaceCloned()
    {
        fixture.Vendor.Documents["ai/prompts/estimate.md"] = Body;
        fixture.Agent.Result = new AgentResult(
            true,
            "13 points, because the API is unknown.",
            null,
            null
        );

        var runId = await Dispatch(_automationId);
        await Execute(runId);

        (await Load(runId)).State.ShouldBe("Succeeded");

        // Verbatim: the product carries no prompt of its own for this action.
        fixture.Agent.Instructions.Single().Prompt.ShouldContain(Body);

        // No comment is asserted any more (#162): the orchestrator posts nothing, and the prompt
        // either did or did not. What must hold instead is that the prompt reached the agent with
        // the project's repository checked out beside it — the old test asserted the exact
        // opposite (no workspace), because the old RepositoryPrompt was a one-comment action.
        fixture.Vendor.Comments.ShouldBeEmpty();
        fixture.Vendor.Stories.Single().State.ShouldBe("open");
        fixture.Workspace.Prepared.ShouldBeTrue();
    }

    [Fact]
    public async Task Frontmatter_Should_NotReachTheAgent()
    {
        fixture.Vendor.Documents["ai/prompts/estimate.md"] =
            $"---\nmodel: some-expensive-model\ntools: [bash, write]\n---\n\n{Body}";
        fixture.Agent.Result = new AgentResult(true, "8 points.", null, null);

        await Execute(await Dispatch(_automationId));

        var prompt = fixture.Agent.Instructions.Single().Prompt;
        prompt.ShouldContain(Body);

        // A model line would let a file in somebody's repository choose what this product spends, and
        // a tool list would let it grant itself powers the Automation withheld.
        prompt.ShouldNotContain("some-expensive-model");
        prompt.ShouldNotContain("tools:");
    }

    [Fact]
    public async Task AMissingPrompt_Should_RefuseNamingTheResolvedPath()
    {
        var runId = await Dispatch(_automationId);
        await Execute(runId);

        var (state, failure) = await Load(runId);
        state.ShouldBe("Failed");
        failure.ShouldNotBeNull();

        // The resolved path, not the name an Admin typed — a misconfigured directory has to be
        // distinguishable from a missing file.
        failure.ShouldContain("ai/prompts/estimate.md");
        fixture.Agent.Instructions.ShouldBeEmpty();
        fixture.Vendor.Comments.ShouldBeEmpty();
    }

    [Fact]
    public async Task APromptWithNoBody_Should_RefuseBeforeTheAgent()
    {
        fixture.Vendor.Documents["ai/prompts/estimate.md"] = "---\nmodel: x\n---\n";

        var runId = await Dispatch(_automationId);
        await Execute(runId);

        var (state, failure) = await Load(runId);
        state.ShouldBe("Failed");
        failure.ShouldNotBeNull();
        failure.ShouldContain("ai/prompts/estimate.md");
        fixture.Agent.Instructions.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheProjectsDirectory_Should_DecideWhereANameResolves()
    {
        // Moved without touching the Automation, which is the point of holding the directory once.
        await Connector(promptDirectory: "prompts/ours");
        fixture.Vendor.Documents["prompts/ours/estimate.md"] = Body;
        fixture.Agent.Result = new AgentResult(true, "5 points.", null, null);

        var runId = await Dispatch(_automationId);
        await Execute(runId);

        (await Load(runId)).State.ShouldBe("Succeeded");
        fixture.Agent.Instructions.Single().Prompt.ShouldContain(Body);
    }

    [Fact]
    public async Task ANameThatLeavesTheDirectory_Should_BeRefused()
    {
        var escaping = await Automation("ai:prompt-escape", "../../.git/config");

        var runId = await Dispatch(escaping);
        await Execute(runId);

        var (state, failure) = await Load(runId);
        state.ShouldBe("Failed");
        failure.ShouldNotBeNull();
        failure.ShouldContain("leaves the prompts directory");
        fixture.Agent.Instructions.ShouldBeEmpty();
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);
}
