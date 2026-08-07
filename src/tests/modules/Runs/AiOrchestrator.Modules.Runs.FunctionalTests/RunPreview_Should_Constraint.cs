using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// run-previews — a preview exists while its Run executes and not one moment longer, and the
/// relay refuses everything it is not for.
/// <para>
/// The fixture's habitat holds no sandboxes, which is the honest default and also the case worth
/// pinning hardest: a portal that cannot host previews must say so rather than imply a Run failed
/// to produce one. The end-to-end "a live Run can be looked at" leg needs a real sandbox and is
/// the documented manual exercise, as the sandboxing change established.
/// </para>
/// </summary>
[Collection(RunsCollection.Name)]
public class RunPreview_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
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

        // The Automation names a port — so everything below is about the habitat and the Run's
        // state, never about the port being absent.
        var automation = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "ai:preview",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "story.md",
                requiresApproval = false,
                previewPort = 8000,
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

    [Fact]
    public async Task AHabitatWithNoSandboxes_Should_SayPreviewsAreNotHostedHere()
    {
        // The distinction the spec insists on: "not available in this habitat" is not the same
        // sentence as "this Run has no preview", and rendering the first as the second would
        // read as the Run having failed.
        var runId = await Dispatch();

        var preview = await _client.GetFromJsonAsync<PreviewResponse>(
            $"/api/projects/{_projectId}/runs/{runId}/preview"
        );

        preview!.Hosted.ShouldBeFalse();
        preview.Available.ShouldBeFalse();
    }

    [Fact]
    public async Task AnUnknownRun_Should_BeNotFound()
    {
        var response = await _client.GetAsync(
            $"/api/projects/{_projectId}/runs/{Guid.CreateVersion7()}/preview"
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheRelay_Should_RefuseARunThatIsNotPreviewing()
    {
        // No sandbox, therefore no port, therefore nothing to serve — and the refusal is what
        // stops a stale or empty frame from looking live.
        var runId = await Dispatch();

        var response = await _client.GetAsync(
            $"/api/projects/{_projectId}/runs/{runId}/preview/serve/index.html"
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheRelay_Should_RefuseARunFromAnotherProject()
    {
        // The relay resolves its target from the ledger by Run id and nowhere else; a caller
        // cannot steer it, and a Run that is not this project's is not found at all.
        var runId = await Dispatch();
        var other = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        var otherProjectId = (await other.Content.ReadFromJsonAsync<ProjectResponse>())!.Id;

        var response = await _client.GetAsync(
            $"/api/projects/{otherProjectId}/runs/{runId}/preview/serve/"
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    internal sealed record PreviewResponse(bool Hosted, bool Available);

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);
}
