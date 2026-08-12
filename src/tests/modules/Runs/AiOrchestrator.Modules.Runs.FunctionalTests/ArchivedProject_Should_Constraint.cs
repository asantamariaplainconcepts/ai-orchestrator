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
/// #121 — archiving stops new work and stops nothing else. The half that makes the feature worth
/// building is the first clause; the half that makes it safe to choose is the second.
/// </summary>
[Collection(RunsCollection.Name)]
public class ArchivedProject_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;
    Guid _automationId;
    string _projectName = string.Empty;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Agent.Reset();
        fixture.Workspace.Reset();
        await fixture.ResetDatabase();
        await fixture.ResetQueue();

        _projectName = $"p-{Guid.NewGuid():N}";
        var created = await _client.PostAsJsonAsync("/api/projects", new { name = _projectName });
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
            }
        );
        automation.EnsureSuccessStatusCode();
        _automationId = (await automation.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;

        fixture.Vendor.Stories.Add(new VendorStory("5", "A story", "open", [], "Body."));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 1);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    Task<HttpResponseMessage> Archive(string confirmName) =>
        _client.PostAsJsonAsync($"/api/projects/{_projectId}/archive", new { confirmName });

    Task<HttpResponseMessage> Restore() =>
        _client.PostAsync($"/api/projects/{_projectId}/restore", null);

    Task<HttpResponseMessage> RunNow() =>
        _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "5", automationId = _automationId }
        );

    async Task<int> RunCount()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        return await database.Runs.CountAsync(run => run.ProjectId == _projectId);
    }

    [Fact]
    public async Task AnArchivedProject_Should_RefuseAManualRun()
    {
        (await Archive(_projectName)).EnsureSuccessStatusCode();

        var refused = await RunNow();

        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).ShouldContain("archived");
        (await RunCount()).ShouldBe(0);
    }

    [Fact]
    public async Task AnArchivedProject_Should_MatchNoLabel()
    {
        (await Archive(_projectName)).EnsureSuccessStatusCode();

        // The label lands at the vendor and reconciliation carries it back, exactly as it would
        // for a live project. Matching is where it stops.
        fixture.Vendor.Stories[0] = fixture.Vendor.Stories[0] with
        {
            Labels = ["ai:refine"],
        };
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 2);

        // Deadline-poll rather than a single read: matching rides CAP's background dispatch, so
        // "no Run yet" and "no Run ever" look identical for a moment.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            (await RunCount()).ShouldBe(0);
            await Task.Delay(200);
        }
    }

    [Fact]
    public async Task ArchivingMidRun_Should_LeaveThatRunAlone()
    {
        fixture.Agent.Result = new AgentResult(true, "A helpful comment.", null, null);
        var started = await RunNow();
        started.EnsureSuccessStatusCode();
        var runId = (await started.Content.ReadFromJsonAsync<RunNowResponse>())!.Id;

        // Archived after the Run exists and before it executes: archiving is not cancellation
        // (UC-014 is), so the work already under way finishes and records itself.
        (await Archive(_projectName)).EnsureSuccessStatusCode();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRunExecutor>().Execute(runId);
        }

        await using var read = fixture.Services.CreateAsyncScope();
        var database = read.ServiceProvider.GetRequiredService<RunsDbContext>();
        var run = await database.Runs.SingleAsync(candidate => candidate.Id == runId);
        run.State.ToString().ShouldBe("Succeeded");
    }

    [Fact]
    public async Task AnArchivedProjects_History_Should_StayReadable()
    {
        fixture.Agent.Result = new AgentResult(true, "A helpful comment.", null, null);
        var started = await RunNow();
        var runId = (await started.Content.ReadFromJsonAsync<RunNowResponse>())!.Id;

        (await Archive(_projectName)).EnsureSuccessStatusCode();

        // Reading stays open; only starting is refused (design D2).
        (await _client.GetAsync($"/api/projects/{_projectId}/runs")).EnsureSuccessStatusCode();
        (
            await _client.GetAsync($"/api/projects/{_projectId}/runs/{runId}/log")
        ).EnsureSuccessStatusCode();
        (await _client.GetAsync($"/api/projects/{_projectId}/pulse")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Restoring_Should_ResumeTheWork()
    {
        (await Archive(_projectName)).EnsureSuccessStatusCode();
        (await Restore()).EnsureSuccessStatusCode();

        var accepted = await RunNow();

        accepted.EnsureSuccessStatusCode();
        (await RunCount()).ShouldBe(1);
    }

    [Fact]
    public async Task ArchivingWithoutTheName_Should_BeRefused()
    {
        var refused = await Archive("not the project's name");

        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        // Nothing changed: the Project still takes work.
        (await RunNow()).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task TheProjectsList_Should_ExcludeArchivedAndCountThem()
    {
        (await Archive(_projectName)).EnsureSuccessStatusCode();

        var listed = await _client.GetFromJsonAsync<ProjectsResponse>("/api/projects");
        listed!.Projects.ShouldNotContain(project => project.Id == _projectId);
        listed.ArchivedCount.ShouldBeGreaterThanOrEqualTo(1);

        var all = await _client.GetFromJsonAsync<ProjectsResponse>(
            "/api/projects?includeArchived=true"
        );
        all!.Projects.ShouldContain(project => project.Id == _projectId);
        all.Projects.Single(project => project.Id == _projectId).ArchivedAt.ShouldNotBeNull();
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record ProjectItem(Guid Id, string Name, DateTimeOffset? ArchivedAt);

    sealed record ProjectsResponse(IReadOnlyList<ProjectItem> Projects, int ArchivedCount);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);
}
