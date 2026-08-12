using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Runs.Features.Conversation;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// UC-033 (#335) — what every visible project has in flight, for the shell's projects tree.
/// <para>
/// The properties worth testing are the ones a reader of the panel relies on: only live work
/// appears, a quiet project is absent rather than empty, a held Story appears with no Run at all,
/// and BR-009 removes a project entirely rather than blanking it. The last is the one that would
/// leak somebody else's backlog if it were wrong, so it asserts on the serialised body — an
/// absent project must not be inferable from what came back.
/// </para>
/// </summary>
[Collection(RunsCollection.Name)]
public class InFlight_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;
    string _projectName = "";
    Guid _refineId;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Agent.Reset();
        fixture.Workspace.Reset();
        await fixture.ResetDatabase();
        await fixture.ResetQueue();

        (_projectId, _projectName) = await CreateProject();
        await Configure(_projectId, "portal");
        _refineId = await CreateAutomation(_projectId, "ai:refine");

        fixture.Vendor.Documents["docs/process/definition-of-ready.md"] = "Needs criteria.";
        fixture.Vendor.Stories.Add(new VendorStory("1", "First story", "open", [], "Body."));
        fixture.Vendor.Stories.Add(new VendorStory("2", "Second story", "open", [], "Body."));
        await Refresh(_projectId);
        await fixture.Probe.WaitForAtLeast(_projectId, 2);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task<(Guid Id, string Name)> CreateProject()
    {
        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        created.EnsureSuccessStatusCode();
        var project = (await created.Content.ReadFromJsonAsync<ProjectResponse>())!;
        return (project.Id, project.Name);
    }

    async Task Configure(Guid projectId, string repository) =>
        (
            await _client.PutAsJsonAsync(
                $"/api/projects/{projectId}/connector",
                new
                {
                    owner = "acme",
                    repository,
                    secretName = "acme-pat",
                }
            )
        ).EnsureSuccessStatusCode();

    async Task Refresh(Guid projectId) =>
        (
            await _client.PostAsync($"/api/projects/{projectId}/backlog/refresh", content: null)
        ).EnsureSuccessStatusCode();

    async Task<Guid> CreateAutomation(Guid projectId, string trigger)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/automations",
            new
            {
                triggerLabel = trigger,
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "story.md",
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;
    }

    /// <summary>Creates a Run and leaves it <c>Queued</c> — dispatch is not execution.</summary>
    async Task<Guid> Queue(string story, Guid automation)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = story, automationId = automation }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RunNowResponse>())!.Id;
    }

    async Task Execute(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRunExecutor>().Execute(runId);
    }

    /// <summary>Applies the hold through the vendor and the ordinary sync, as a person would.</summary>
    async Task Hold(string vendorStoryId) =>
        (
            await _client.PutAsync(
                $"/api/projects/{_projectId}/backlog/stories/{vendorStoryId}/labels/hitl",
                content: null
            )
        ).EnsureSuccessStatusCode();

    async Task<InFlightResponse> InFlight() =>
        (await _client.GetFromJsonAsync<InFlightResponse>("/api/in-flight"))!;

    async Task<string> InFlightBody() =>
        await (await _client.GetAsync("/api/in-flight")).Content.ReadAsStringAsync();

    void AgentSays(bool ok, string reply) =>
        fixture.Agent.Result = new AgentResult(ok, reply, null, null);

    [Fact]
    public async Task AQueuedRun_Should_AppearUnderItsStory()
    {
        var queued = await Queue("1", _refineId);

        var project = (await InFlight()).Projects.ShouldHaveSingleItem();

        project.ProjectId.ShouldBe(_projectId);
        project.ProjectName.ShouldBe(_projectName);

        var work = project.Work.ShouldHaveSingleItem();
        work.VendorStoryId.ShouldBe("1");
        work.Title.ShouldBe("First story");
        work.Held.ShouldBeFalse();
        work.ChangeNumber.ShouldBeNull();

        var run = work.Runs.ShouldHaveSingleItem();
        run.RunId.ShouldBe(queued);
        run.State.ShouldBe("Queued");
    }

    /// <summary>
    /// The majority of what this surface adds over the per-project Runs list: work that is waiting
    /// on a person and has no Run at all (DEC-067).
    /// </summary>
    [Fact]
    public async Task AHeldStory_Should_AppearWithNoRun()
    {
        await Hold("1");

        var work = (await InFlight()).Projects.ShouldHaveSingleItem().Work.ShouldHaveSingleItem();

        work.VendorStoryId.ShouldBe("1");
        work.Title.ShouldBe("First story");
        work.Held.ShouldBeTrue();
        work.Runs.ShouldBeEmpty();
    }

    /// <summary>
    /// A hold and a Run are two facts about one subject, so they share a node rather than making
    /// the Story appear twice. The hold arrives after the Run because BR-007 gates creation, not
    /// execution — a hold applied first would refuse the Run.
    /// </summary>
    [Fact]
    public async Task AStoryBothHeldAndRunning_Should_BeOneNode()
    {
        var queued = await Queue("1", _refineId);
        await Hold("1");

        var work = (await InFlight()).Projects.ShouldHaveSingleItem().Work.ShouldHaveSingleItem();

        work.VendorStoryId.ShouldBe("1");
        work.Held.ShouldBeTrue();
        work.Runs.ShouldHaveSingleItem().RunId.ShouldBe(queued);
    }

    /// <summary>
    /// The subtraction that keeps the panel about *live* work — and the difference from the Inbox,
    /// which still shows the failure because a person has not decided about it.
    /// </summary>
    [Fact]
    public async Task ATerminalRun_Should_LeaveTheTree()
    {
        AgentSays(false, "boom");
        var failed = await Queue("1", _refineId);
        await Execute(failed);

        (await InFlight()).Projects.ShouldBeEmpty();

        // Not lost, just not "in flight": the Inbox is where an undismissed failure waits.
        (await _client.GetFromJsonAsync<List<InboxRow>>("/api/inbox"))!
            .ShouldHaveSingleItem()
            .RunId.ShouldBe(failed);
    }

    [Fact]
    public async Task AQuietProject_Should_BeAbsentNotEmpty()
    {
        // Nothing queued and nothing held: the project exists, has a Connector and two mirrored
        // Stories, and still contributes no entry.
        (await InFlight()).Projects.ShouldBeEmpty();
    }

    [Fact]
    public async Task NoProjects_Should_BeAnEmptyListNotAnError()
    {
        fixture.Permissions.Visible = new HashSet<Guid>();

        var response = await _client.GetAsync("/api/in-flight");

        response.EnsureSuccessStatusCode();
        (await response.Content.ReadFromJsonAsync<InFlightResponse>())!.Projects.ShouldBeEmpty();
    }

    /// <summary>
    /// AC 5 of #335, and the reason this endpoint is scoped at all: an invisible project is
    /// <b>absent</b>. Asserted against the raw body, because "present but empty" and "leaked the
    /// name" are both failures a typed assertion on the visible project alone would pass.
    /// </summary>
    [Fact]
    public async Task AnInvisibleProject_Should_BeAbsentFromTheResponse()
    {
        await Queue("1", _refineId);

        var (otherId, otherName) = await CreateProject();
        await Configure(otherId, "secret-repo");
        var otherAutomation = await CreateAutomation(otherId, "ai:refine");
        fixture.Vendor.Stories.Add(
            new VendorStory("9", "Somebody else's story", "open", [], "Body.")
        );
        await Refresh(otherId);
        await fixture.Probe.WaitForAtLeast(otherId, 1);
        (
            await _client.PostAsJsonAsync(
                $"/api/projects/{otherId}/runs",
                new { vendorStoryId = "9", automationId = otherAutomation }
            )
        ).EnsureSuccessStatusCode();

        // Both projects have live work; the caller may see only one.
        fixture.Permissions.Visible = new HashSet<Guid> { _projectId };

        var response = await InFlight();
        var body = await InFlightBody();

        response.Projects.ShouldHaveSingleItem().ProjectId.ShouldBe(_projectId);
        body.ShouldNotContain(otherId.ToString());
        body.ShouldNotContain(otherName);
        body.ShouldNotContain("Somebody else's story");
    }

    /// <summary>
    /// AC 6: the tree must not move the Inbox's ambient count. The count is <c>length</c> over the
    /// Inbox array, so a Queued Run — which this surface reports and the Inbox must not — is the
    /// case that would break it.
    /// </summary>
    [Fact]
    public async Task LiveWork_Should_NotReachTheInbox()
    {
        var before = await _client.GetStringAsync("/api/inbox");

        await Queue("1", _refineId);
        await Hold("2");

        (await InFlight()).Projects.ShouldHaveSingleItem().Work.Count.ShouldBe(2);

        (await _client.GetStringAsync("/api/inbox")).ShouldBe(before);
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);

    sealed record InboxRow(Guid RunId);

    sealed record InFlightResponse(IReadOnlyList<ProjectEntry> Projects);

    sealed record ProjectEntry(Guid ProjectId, string? ProjectName, IReadOnlyList<WorkEntry> Work);

    sealed record WorkEntry(
        string? VendorStoryId,
        string? Title,
        bool Held,
        int? ChangeNumber,
        IReadOnlyList<RunEntry> Runs
    );

    sealed record RunEntry(Guid RunId, string State, DateTimeOffset CreatedAt);
}
