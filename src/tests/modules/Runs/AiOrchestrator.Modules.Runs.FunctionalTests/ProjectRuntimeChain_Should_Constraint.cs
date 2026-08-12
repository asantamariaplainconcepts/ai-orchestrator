using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// project-runtimes (#244), design D2 — one resolution chain with one order: the human's per-Run
/// choice, then the Automation's explicit runtime, then the Project default, then the deployment
/// default; and the AI credential resolves project name → deployment name → none, with the
/// transcript naming the source.
/// </summary>
[Collection(RunsCollection.Name)]
public class ProjectRuntimeChain_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
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

    Task<HttpResponseMessage> PutSettings(
        string? defaultRuntime,
        Dictionary<string, string>? credentialNames = null
    ) =>
        _client.PutAsJsonAsync(
            $"/api/projects/{_projectId}/runtimes",
            new { defaultRuntime, credentialNames = credentialNames ?? [] }
        );

    async Task<Guid> CreateAutomation(string? runtime, string label = "ai:implement")
    {
        var automation = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = label,
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                promptPath = "story.md",
                runtime,
            }
        );
        automation.EnsureSuccessStatusCode();
        return (await automation.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;
    }

    async Task<Guid> RunNow(Guid automationId, string? runtime = null)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new
            {
                vendorStoryId = "9",
                automationId,
                runtime,
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

    async Task<string> Log(Guid runId)
    {
        var log = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{_projectId}/runs/{runId}/log"
        );
        return log.GetProperty("content").GetString()!;
    }

    [Fact]
    public async Task ARuntimelessAutomation_Should_ResolveToTheProjectDefault()
    {
        (await PutSettings("OpenCode")).EnsureSuccessStatusCode();
        var automationId = await CreateAutomation(runtime: null);

        await Execute(await RunNow(automationId));

        // The project default decided the implementation; the deployment default never ran.
        fixture.OpenCodeAgent.Instructions.Count.ShouldBe(1);
        fixture.Agent.Instructions.ShouldBeEmpty();
    }

    [Fact]
    public async Task ThePerRunChoice_Should_OutrankTheAutomationsRuntime()
    {
        var automationId = await CreateAutomation("OpenCode");

        await Execute(await RunNow(automationId, runtime: "ClaudeCodeHeadless"));

        // The launch dialog said "for this Run" — losing to the Automation's value would make
        // that a lie (design D2). And the choice is for that Run only: the Automation keeps its
        // own runtime for the next one.
        fixture.Agent.Instructions.Count.ShouldBe(1);
        fixture.OpenCodeAgent.Instructions.ShouldBeEmpty();

        var automations = await _client.GetFromJsonAsync<List<JsonElement>>(
            $"/api/projects/{_projectId}/automations"
        );
        automations!.Single().GetProperty("runtime").GetString().ShouldBe("OpenCode");
    }

    [Fact]
    public async Task WithNothingChosenAnywhere_Should_FallToTheDeploymentDefault()
    {
        var automationId = await CreateAutomation(runtime: null);

        await Execute(await RunNow(automationId));

        // No per-Run choice, no Automation runtime, no project default: the chain's last link.
        fixture.Agent.Instructions.Count.ShouldBe(1);
        fixture.OpenCodeAgent.Instructions.ShouldBeEmpty();
    }

    [Fact]
    public async Task ALabelTriggeredRun_Should_TakeTheProjectDefaultWithoutAnyOverride()
    {
        (await PutSettings("OpenCode")).EnsureSuccessStatusCode();
        await CreateAutomation(runtime: null, label: "ai:refine");

        // The label lands and matching creates the Run — no launch dialog anywhere in this path,
        // so the Run records no choice and the project default decides at execution time.
        fixture.Vendor.Stories[0] = new VendorStory("9", "A story", "open", ["ai:refine"]);
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 2);

        var runs = await _client.GetFromJsonAsync<JsonElement>($"/api/projects/{_projectId}/runs");
        var runId = runs.EnumerateArray().Single().GetProperty("id").GetGuid();

        await Execute(runId);

        fixture.OpenCodeAgent.Instructions.Count.ShouldBe(1);
        fixture.Agent.Instructions.ShouldBeEmpty();
    }

    [Fact]
    public async Task AProjectCredentialName_Should_OutrankTheDeployments()
    {
        (
            await PutSettings(
                null,
                new Dictionary<string, string> { ["ClaudeCodeHeadless"] = "acme-anthropic" }
            )
        ).EnsureSuccessStatusCode();
        var automationId = await CreateAutomation("ClaudeCodeHeadless");
        var runId = await RunNow(automationId);
        fixture.SecretNames.Clear();

        await Execute(runId);

        // The project's name reached the vault; the deployment's never did (BR-010 — names
        // resolved, values never surfaced). The transcript names the source, because a Run
        // billed to the wrong key must be diagnosable from its own record.
        fixture.SecretNames.All.ShouldContain("acme-anthropic");
        fixture.SecretNames.All.ShouldNotContain("anthropic-api-key");
        (await Log(runId)).ShouldStartWith(
            "Runtime 'ClaudeCodeHeadless' — credential source: project, carried in the agent process's environment."
        );
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);
}
