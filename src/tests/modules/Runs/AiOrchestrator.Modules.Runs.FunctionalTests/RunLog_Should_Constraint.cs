using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// UC-027 — output is persisted while the Run executes, and readable with a done-flag. The
/// stub runtime forwards its scripted transcript line by line, so these tests drive the real
/// writer: channel, batching, tail-flush on dispose.
/// </summary>
[Collection(RunsCollection.Name)]
public class RunLog_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
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
                triggerLabel = "ai:refine",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "story.md",
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

    Task<LogResponse?> Log(Guid runId) =>
        _client.GetFromJsonAsync<LogResponse>($"/api/projects/{_projectId}/runs/{runId}/log");

    [Fact]
    public async Task AFinishedRun_Should_ServeItsFullTranscriptMarkedComplete()
    {
        fixture.Agent.Result = new AgentResult(
            true,
            "reading the story\nweighing options\nwriting the comment",
            null,
            null
        );

        var runId = await DispatchAndExecute();

        var log = await Log(runId);
        log!.Complete.ShouldBeTrue();
        log.Content.ShouldBe("reading the story\nweighing options\nwriting the comment");
    }

    [Fact]
    public async Task ACrashMidRun_Should_PreserveTheLinesWrittenBeforeIt()
    {
        // The runtime emits its lines and then the executor's pipeline dies: the transcript
        // written so far must survive, because the durable store IS the stream (design D1).
        fixture.Agent.Result = new AgentResult(true, "step one\nstep two", null, null);
        fixture.Agent.Throws = new InvalidOperationException("runtime died");

        var runId = await DispatchAndExecute();

        var log = await Log(runId);
        log!.Complete.ShouldBeTrue(); // the crash failed the Run — terminal, honestly
        log.Content.ShouldContain("step one");
        log.Content.ShouldContain("step two");
    }

    [Fact]
    public async Task AnUnknownRun_Should_BeNotFound()
    {
        var response = await _client.GetAsync(
            $"/api/projects/{_projectId}/runs/{Guid.CreateVersion7()}/log"
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ARunWithNoOutput_Should_ServeAnEmptyCompleteLog()
    {
        fixture.Agent.Result = new AgentResult(true, "", null, null);

        var runId = await DispatchAndExecute();

        var log = await Log(runId);
        log!.Complete.ShouldBeTrue();
        log.Content.ShouldBe(string.Empty);
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);

    sealed record LogResponse(string Content, bool Complete);
}
