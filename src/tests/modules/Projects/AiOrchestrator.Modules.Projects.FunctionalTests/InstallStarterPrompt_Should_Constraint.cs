using System.Net;
using System.Net.Http.Json;
using ErrorOr;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// #214 — one bounded git write: presence re-checked at click time, a starter-scoped branch, a
/// draft PR, and refusals that name what the Admin can do about them. The default branch is the
/// stub's problem to prove untouched: the workspace only ever sees its own clone.
/// </summary>
[Collection(ProjectsCollection.Name)]
public class InstallStarterPrompt_Should_Constraint(ProjectsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    readonly Guid _projectId = Guid.CreateVersion7();

    public async Task InitializeAsync() => await fixture.ResetDatabase();

    public Task DisposeAsync() => Task.CompletedTask;

    Task<HttpResponseMessage> Install(string saveAs) =>
        _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/starter-prompts/install",
            new { saveAs }
        );

    [Fact]
    public async Task Install_Should_OpenADraftPullRequestOnAStarterScopedBranch()
    {
        var response = await Install("aio-grill.md");

        response.EnsureSuccessStatusCode();
        var body = (await response.Content.ReadFromJsonAsync<InstallResponse>())!;

        body.Url.ShouldBe("https://github.com/acme/portal/pull/7");
        body.Path.ShouldBe("ai/prompts/aio-grill.md");
        body.Branch.ShouldBe("starter/aio-grill");

        fixture.Workspace.PreparedBranch.ShouldBe("starter/aio-grill");
        // The spec's whole point: a human merges, so the PR must be born a draft.
        fixture.Workspace.PublishedAsDraft.ShouldBe(true);
        fixture.Workspace.PublishedFiles.ShouldBe(["ai/prompts/aio-grill.md"]);
    }

    [Fact]
    public async Task Install_Should_RefuseByNameWhenTheFileAlreadyExists()
    {
        fixture.Documents.Documents["ai/prompts/aio-grill.md"] = "already here";

        var response = await Install("aio-grill.md");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldContain("ai/prompts/aio-grill.md");

        // Refused before any workspace exists — no branch, no PR.
        fixture.Workspace.PreparedBranch.ShouldBeNull();
    }

    [Fact]
    public async Task Install_Should_RefuseWithoutAConnector()
    {
        fixture.Documents.Connected = false;
        fixture.Connector.Snapshot = null;

        var response = await Install("aio-grill.md");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        fixture.Workspace.PreparedBranch.ShouldBeNull();
    }

    [Fact]
    public async Task Install_Should_RefuseAnUnknownStarter()
    {
        var response = await Install("not-a-starter.md");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Install_Should_NameTheFailingStage()
    {
        fixture.Workspace.PublishError = Error.Failure(
            "Workspace.PushFailed",
            "Pushing the run branch failed: rejected"
        );

        var response = await Install("aio-grill.md");

        response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("PushFailed");
    }

    sealed record InstallResponse(string Url, string Path, string Branch);
}
