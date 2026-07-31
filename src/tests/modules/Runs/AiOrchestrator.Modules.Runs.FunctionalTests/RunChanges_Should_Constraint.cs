using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.Modules.Backlog.Connectors;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// UC-024: what the Agent changed, read live at the Run's linked change. The three absences —
/// no pull request, a change touching nothing, a file whose patch cannot be shown — are three
/// different answers, because a reviewer acts differently on each.
/// </summary>
[Collection(RunsCollection.Name)]
public class RunChanges_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
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
                triggerLabel = "ai:implement",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
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

    async Task<Guid> Dispatch()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "9", automationId = _automationId }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RunNowResponse>())!.Id;
    }

    Task<ChangesResponse?> Changes(Guid runId) =>
        _client.GetFromJsonAsync<ChangesResponse>(
            $"/api/projects/{_projectId}/runs/{runId}/changes"
        );

    [Fact]
    public async Task Changes_Should_ReportEachFileWithItsPatchAndCounts()
    {
        var runId = await Dispatch();
        fixture.Vendor.Change = new LinkedChange(
            42,
            "feat",
            "https://example.invalid/pull/42",
            "sha"
        );
        fixture.Vendor.Files.Add(
            new ChangedFile("src/Thing.cs", "modified", 12, 3, "@@ -1 +1 @@\n-old\n+new", null)
        );

        var changes = await Changes(runId);

        changes!.Change.ShouldNotBeNull();
        changes.Change.Number.ShouldBe(42);
        var file = changes.Change.Files.Single();
        file.Path.ShouldBe("src/Thing.cs");
        file.Additions.ShouldBe(12);
        file.Deletions.ShouldBe(3);
        file.Patch!.ShouldContain("+new");
        file.PatchOmittedReason.ShouldBeNull();
    }

    [Fact]
    public async Task AnUnshowableFile_Should_StateItsReasonInsteadOfATruncatedPatch()
    {
        var runId = await Dispatch();
        fixture.Vendor.Change = new LinkedChange(42, "feat", "https://example.invalid/42", "sha");
        fixture.Vendor.Files.Add(
            new ChangedFile("docs/logo.png", "added", 0, 0, null, PatchOmission.Binary)
        );
        fixture.Vendor.Files.Add(
            new ChangedFile("data/dump.sql", "added", 90000, 0, null, PatchOmission.TooLarge)
        );

        var changes = await Changes(runId);

        var files = changes!.Change!.Files;
        files.Single(file => file.Path == "docs/logo.png").PatchOmittedReason.ShouldBe("Binary");
        files.Single(file => file.Path == "data/dump.sql").PatchOmittedReason.ShouldBe("TooLarge");
        // The point of the reason: no half-diff is presented as the whole change.
        files.ShouldAllBe(file => file.Patch == null);
    }

    [Fact]
    public async Task ARunWithNoPullRequest_Should_AnswerWithAnExplicitAbsence()
    {
        var runId = await Dispatch();

        var changes = await Changes(runId);

        // Not a 404: the Run exists, it simply has produced no change yet.
        changes!.Change.ShouldBeNull();
    }

    [Fact]
    public async Task AnUnknownRun_Should_Be404()
    {
        var response = await _client.GetAsync(
            $"/api/projects/{_projectId}/runs/{Guid.CreateVersion7()}/changes"
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);

    sealed record FileResponse(
        string Path,
        string Status,
        int Additions,
        int Deletions,
        string? Patch,
        string? PatchOmittedReason
    );

    sealed record ChangeResponse(int Number, string Url, IReadOnlyList<FileResponse> Files);

    sealed record ChangesResponse(ChangeResponse? Change);
}
