using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// The opencode-runtime spec: the Automation's runtime decides which implementation executes,
/// and a runtime whose credential name is absent performs no vault lookup at all — free
/// providers are configuration, not an error path.
/// </summary>
[Collection(RunsCollection.Name)]
public class RuntimeSelection_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
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

    async Task<Guid> DispatchWith(string runtime, string label)
    {
        var automation = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = label,
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                rubricPath = "task.md",
                runtime,
                requiresApproval = false,
            }
        );
        automation.EnsureSuccessStatusCode();
        var automationId = (await automation.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "9", automationId }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RunNowResponse>())!.Id;
    }

    async Task Execute(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRunExecutor>().Execute(runId);
    }

    [Fact]
    public async Task TheAutomationsRuntime_Should_DecideTheImplementation()
    {
        var runId = await DispatchWith("OpenCode", "oc:implement");

        await Execute(runId);

        // The OpenCode fake executed; the Claude Code fake never saw an instruction.
        fixture.OpenCodeAgent.Instructions.Count.ShouldBe(1);
        fixture.Agent.Instructions.ShouldBeEmpty();
    }

    [Fact]
    public async Task AFreeModelRuntime_Should_ResolveNoAiCredential()
    {
        var runId = await DispatchWith("OpenCode", "oc:implement");
        fixture.SecretNames.Clear();

        await Execute(runId);

        // Only the PAT was resolved — no AI credential name reached the vault, and the fake
        // received an empty key.
        // Twice, and that is the change rather than a leak: since #162 every Run reads its prompt from
        // the repository before the agent starts, and that read is itself a credentialled vendor call.
        // BR-010 asks for resolution per read, so two reads resolve twice — the *names* are what matter.
        fixture.SecretNames.All.ShouldBe(["acme-pat", "acme-pat"]);
        fixture.OpenCodeAgent.Instructions.Single().Credentials.AiApiKey.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task TheClaudeCodePath_Should_StillResolveItsCredential()
    {
        var runId = await DispatchWith("ClaudeCodeHeadless", "cc:implement");
        fixture.SecretNames.Clear();

        await Execute(runId);

        fixture.SecretNames.All.ShouldBe(["acme-pat", "anthropic-api-key", "acme-pat"]);
        fixture.Agent.Instructions.Single().Credentials.AiApiKey.ShouldBe("stub-token");
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);
}
