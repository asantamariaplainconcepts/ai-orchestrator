using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.Modules.Backlog.Connectors;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// run-on-a-pr — the Run without a Story. What must hold: the vendor answers what a number means
/// (URL and branch never come from the caller), the instruction is recorded on the Run, one
/// active Run per change mirrors BR-001 without contending with story Runs, and an empty
/// instruction dies at the edge.
/// </summary>
[Collection(RunsCollection.Name)]
public class RunOnChange_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Agent.Reset();
        fixture.Workspace.Reset();
        await fixture.ResetDatabase();
        await fixture.ResetQueue();

        _projectId = await CreateProject();
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

        fixture.Vendor.Open.Add(
            new OpenChange(
                118,
                "feat: the estimate explains itself",
                "https://github.com/acme/portal/pull/118",
                "feature/estimate-notes",
                DateTimeOffset.UtcNow
            )
        );
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ALaunch_Should_RecordTheInstructionAndTheVendorsAnswer()
    {
        var response = await Launch(118, "apply the review comments about naming");
        response.EnsureSuccessStatusCode();

        var runs = await Runs();
        var run = runs.ShouldHaveSingleItem();

        // The record: the instruction, and the target as the vendor answered it — never as a
        // caller claimed it.
        run.Instruction.ShouldBe("apply the review comments about naming");
        run.TargetChangeNumber.ShouldBe(118);
        run.TargetChangeUrl.ShouldBe("https://github.com/acme/portal/pull/118");
        run.VendorStoryId.ShouldBeNull();
        run.AutomationId.ShouldBeNull();

        // And no Automation was created: ad-hoc text creates a Run, not configuration.
        var automations = await _client.GetFromJsonAsync<List<object>>(
            $"/api/projects/{_projectId}/automations"
        );
        automations!.ShouldBeEmpty();
    }

    [Fact]
    public async Task ASecondLaunchOnTheSameChange_Should_BeRefusedNamingTheRule()
    {
        (await Launch(118, "first")).EnsureSuccessStatusCode();

        var second = await Launch(118, "second");

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await second.Content.ReadAsStringAsync()).ShouldContain("active Run");

        (await Runs()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task AStoryRun_Should_NotBlockAChangeRun()
    {
        // An active story Run on the project — the change rule is scoped to the change,
        // mirroring BR-001's scope to the Story.
        await SeedStoryRun();

        var response = await Launch(118, "iterate the PR");

        response.EnsureSuccessStatusCode();
        (await Runs()).Count.ShouldBe(2);
    }

    [Fact]
    public async Task AnEmptyInstruction_Should_DieAtTheEdge()
    {
        var response = await Launch(118, "   ");

        ((int)response.StatusCode).ShouldBe(400);
        (await Runs()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ANumberTheVendorDoesNotList_Should_BeRefused()
    {
        var response = await Launch(999, "do something");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Runs()).ShouldBeEmpty();
    }

    Task<HttpResponseMessage> Launch(int number, string instruction) =>
        _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/changes/{number}/runs",
            new { instruction }
        );

    async Task<List<RunRow>> Runs()
    {
        var listed = await _client.GetFromJsonAsync<List<RunRow>>(
            $"/api/projects/{_projectId}/runs"
        );
        return listed!;
    }

    async Task SeedStoryRun()
    {
        var automation = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "ai:implement",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "story.md",
            }
        );
        automation.EnsureSuccessStatusCode();
        var created = (await automation.Content.ReadFromJsonAsync<AutomationResponse>())!;

        fixture.Vendor.Stories.Add(new("41", "A story", "open", [], "Body."));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 1);

        (
            await _client.PostAsJsonAsync(
                $"/api/projects/{_projectId}/runs",
                new { vendorStoryId = "41", automationId = created.Id }
            )
        ).EnsureSuccessStatusCode();
    }

    async Task<Guid> CreateProject()
    {
        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        created.EnsureSuccessStatusCode();
        var project = (await created.Content.ReadFromJsonAsync<ProjectResponse>())!;
        return project.Id;
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunRow(
        Guid Id,
        string? VendorStoryId,
        Guid? AutomationId,
        string State,
        int? TargetChangeNumber,
        string? TargetChangeUrl,
        string? Instruction
    );
}
