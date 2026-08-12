using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #291, design D4 — the model resolves beside the runtime and one level shorter: the human's
/// per-Run choice, then the Automation's, then whatever the runtime already defaults to. There is
/// no Project level, by scope.
/// <para>
/// Asserted on what the runtime was actually handed, not on what was stored, because the claim is
/// about what the agent thinks with. The fake runtime records its instruction, so a chain that
/// resolved correctly and then failed to pass the value along still fails here.
/// </para>
/// </summary>
[Collection(RunsCollection.Name)]
public class ModelChain_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Agent.Reset();
        fixture.OpenCodeAgent.Reset();
        fixture.Workspace.Reset();
        fixture.SecretNames.Clear();
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

        fixture.Vendor.Stories.Add(new VendorStory("9", "A story", "open", []));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 1);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AnAutomationsModel_Should_ReachTheAgent()
    {
        var automationId = await CreateAutomation(model: "github-copilot/claude-opus-4.6");

        await Execute(await RunNow(automationId));

        Handed().Model.ShouldBe("github-copilot/claude-opus-4.6");
    }

    [Fact]
    public async Task ThePerRunChoice_Should_OutrankTheAutomationsModel()
    {
        var automationId = await CreateAutomation(model: "opencode/cheap");

        await Execute(await RunNow(automationId, model: "github-copilot/claude-opus-4.6"));

        // The dialog says "for this Run only"; losing to the Automation would make that a lie.
        Handed().Model.ShouldBe("github-copilot/claude-opus-4.6");

        // And it is for that Run only: the Automation keeps its own for the next one.
        var automations = await _client.GetFromJsonAsync<List<JsonElement>>(
            $"/api/projects/{_projectId}/automations"
        );
        automations!.Single().GetProperty("model").GetString().ShouldBe("opencode/cheap");
    }

    [Fact]
    public async Task WithNothingChosenAnywhere_Should_LaunchExactlyAsBefore()
    {
        // The whole no-op guarantee (#291's AC2). A deployment that chose nothing must hand the
        // runtime null, so the CLI keeps whatever default it had before this change existed —
        // never an empty string, which would name a model called nothing.
        var automationId = await CreateAutomation(model: null);

        await Execute(await RunNow(automationId));

        Handed().Model.ShouldBeNull();
    }

    [Fact]
    public async Task ABlankModel_Should_MeanInheritRatherThanAModelNamedNothing()
    {
        // A form that submits an empty field means "inherit". Storing "" would resolve to a model
        // named nothing at execution time, which no CLI can be asked for.
        var automationId = await CreateAutomation(model: "   ");

        await Execute(await RunNow(automationId, model: "  "));

        Handed().Model.ShouldBeNull();
    }

    [Fact]
    public async Task AFailedRun_Should_SayWhichModelItWasThinkingWith()
    {
        // opencode answers an unknown model with "Unexpected server error" and names nothing
        // (observed 2026-08-08), so the product has to say it or nobody will.
        fixture.Agent.Result = new AgentResult(
            Succeeded: false,
            Log: "the agent refused",
            OutputLink: null,
            Usage: null
        );
        var automationId = await CreateAutomation(model: "a-model-that-does-not-exist");

        var runId = await RunNow(automationId);
        await Execute(runId);

        var runs = await _client.GetFromJsonAsync<List<JsonElement>>(
            $"/api/projects/{_projectId}/runs"
        );
        var failure = runs!.Single(run => run.GetProperty("id").GetGuid() == runId);

        failure
            .GetProperty("failureReason")
            .GetString()
            .ShouldNotBeNull()
            .ShouldContain("a-model-that-does-not-exist");
        // And the Run records what it ran on, so the cost beside it can be interpreted.
        failure.GetProperty("resolvedModel").GetString().ShouldBe("a-model-that-does-not-exist");
    }

    AgentInstruction Handed() => fixture.Agent.Instructions.ShouldHaveSingleItem();

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);

    async Task<Guid> CreateAutomation(string? model, string label = "ai:implement")
    {
        var automation = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = label,
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                promptPath = "story.md",
                runtime = "ClaudeCodeHeadless",
                model,
            }
        );
        automation.EnsureSuccessStatusCode();
        return (await automation.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;
    }

    async Task<Guid> RunNow(Guid automationId, string? model = null)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new
            {
                vendorStoryId = "9",
                automationId,
                model,
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RunNowResponse>())!.Id;
    }

    async Task Execute(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRunExecutor>().Execute(runId);
    }
}
