using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// UC-025 — a ready Story becomes a documentation PR. The refusals carry the value: no body
/// means nothing to propose from, and an existing linked change is named, never duplicated.
/// Both happen before any workspace exists.
/// </summary>
[Collection(RunsCollection.Name)]
public class ProposeAction_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
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
                triggerLabel = "ai:propose",
                triggerState = (string?)null,
                action = "ProposeSpec",
                runtime = "ClaudeCodeHeadless",
                requiresApproval = false,
            }
        );
        automation.EnsureSuccessStatusCode();
        _automationId = (await automation.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task SeedStory(string? body)
    {
        fixture.Vendor.Stories.Add(new VendorStory("9", "A ready story", "open", [], body));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 1);
    }

    async Task<Guid> DispatchAndExecute()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "9", automationId = _automationId }
        );
        response.EnsureSuccessStatusCode();
        var runId = (await response.Content.ReadFromJsonAsync<RunNowResponse>())!.Id;

        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRunExecutor>().Execute(runId);
        return runId;
    }

    async Task<(string State, string? Failure, string? Output)> Load(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        return await database
            .Runs.Where(run => run.Id == runId)
            .Select(run => new ValueTuple<string, string?, string?>(
                run.State.ToString(),
                run.FailureReason,
                run.OutputLink
            ))
            .SingleAsync();
    }

    [Fact]
    public async Task AReadyStory_Should_BecomeADocumentationPullRequest()
    {
        await SeedStory("As an admin I want defaults so that setup is one click.");

        var runId = await DispatchAndExecute();

        var (state, _, output) = await Load(runId);
        state.ShouldBe("Succeeded");
        output.ShouldNotBeNull();
        fixture.Workspace.Published.ShouldBeTrue();

        // The prompt carried the proposal contract, not the implement one.
        var prompt = fixture.Agent.Instructions.Single().Prompt;
        prompt.ShouldContain("proposal");
        prompt.ShouldContain("docs/proposals/story-9/");
        prompt.ShouldContain("change no code");
    }

    [Fact]
    public async Task AStoryWithNoBody_Should_BeRefusedBeforeAnyWorkspace()
    {
        await SeedStory(body: null);

        var runId = await DispatchAndExecute();

        var (state, failure, _) = await Load(runId);
        state.ShouldBe("Failed");
        failure!.ShouldContain("nothing to propose from");

        // Refused before spending: no clone, no agent call.
        fixture.Workspace.Prepared.ShouldBeFalse();
        fixture.Agent.Instructions.ShouldBeEmpty();
    }

    [Fact]
    public async Task AStoryWithAnOpenChange_Should_BeRefusedNamingIt()
    {
        await SeedStory("A described story.");
        fixture.Vendor.Change = new LinkedChange(
            41,
            "Existing proposal",
            "https://example.test/pr/41",
            "change/existing"
        );

        var runId = await DispatchAndExecute();

        var (state, failure, _) = await Load(runId);
        state.ShouldBe("Failed");
        failure!.ShouldContain("https://example.test/pr/41");
        fixture.Workspace.Prepared.ShouldBeFalse();
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);
}
