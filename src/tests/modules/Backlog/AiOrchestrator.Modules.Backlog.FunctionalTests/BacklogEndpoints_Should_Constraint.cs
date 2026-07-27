using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Domain;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.FunctionalTests;

/// <summary>
/// Configure → poll → read, against real containers with the vendor stubbed at the seam.
/// </summary>
[Collection(BacklogCollection.Name)]
public class BacklogEndpoints_Should_Constraint(BacklogApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    readonly Guid _projectId = Guid.CreateVersion7();

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        await fixture.ResetDatabase();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task Configure(string secretName = "acme-pat") =>
        (
            await _client.PutAsJsonAsync(
                $"/api/projects/{_projectId}/connector",
                new
                {
                    owner = "acme",
                    repository = "portal",
                    secretName,
                }
            )
        ).EnsureSuccessStatusCode();

    Task<HttpResponseMessage> Refresh() =>
        _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", content: null);

    async Task<BacklogResponse> Read() =>
        (await _client.GetFromJsonAsync<BacklogResponse>($"/api/projects/{_projectId}/backlog"))!;

    [Fact]
    public async Task Configure_Should_StoreTheSecretNameAndNeverAToken()
    {
        await Configure();

        var backlog = await Read();

        backlog.Connector.ShouldNotBeNull();
        backlog.Connector.SecretName.ShouldBe("acme-pat");

        // BR-010: nothing anywhere in the response may carry a credential.
        var raw = await _client.GetStringAsync($"/api/projects/{_projectId}/backlog");
        raw.ShouldNotContain("stub-token");
    }

    [Fact]
    public async Task Configure_Should_RejectACredentialTheVendorRefuses()
    {
        fixture.Vendor.VerifyError = BacklogErrors.CredentialRejected("acme-pat");

        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{_projectId}/connector",
            new
            {
                owner = "acme",
                repository = "portal",
                secretName = "acme-pat",
            }
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("CredentialRejected");

        // Verification failing must leave nothing behind.
        (await Read()).Connector.ShouldBeNull();
    }

    [Fact]
    public async Task Configure_Should_ReportAnUnknownRepositoryDistinctlyFromABadCredential()
    {
        fixture.Vendor.VerifyError = BacklogErrors.RepositoryNotFound("acme", "nope");

        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{_projectId}/connector",
            new
            {
                owner = "acme",
                repository = "nope",
                secretName = "acme-pat",
            }
        );

        // The two failures have different fixes, so they must not collapse into one message.
        (await response.Content.ReadAsStringAsync()).ShouldContain("RepositoryNotFound");
    }

    [Fact]
    public async Task Configure_Should_FailWhenTheNamedSecretDoesNotExist()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{_projectId}/connector",
            new
            {
                owner = "acme",
                repository = "portal",
                secretName = "missing-secret",
            }
        );

        (await response.Content.ReadAsStringAsync()).ShouldContain("SecretNotFound");
    }

    [Fact]
    public async Task Configure_Should_ReplaceRatherThanAddASecondConnector()
    {
        await Configure();
        await _client.PutAsJsonAsync(
            $"/api/projects/{_projectId}/connector",
            new
            {
                owner = "acme",
                repository = "other",
                secretName = "acme-pat",
            }
        );

        (await Read()).Connector!.Repository.ShouldBe("other");
    }

    [Fact]
    public async Task Refresh_Should_MirrorTheVendorsStories()
    {
        await Configure();
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", ["bug"]));
        fixture.Vendor.Stories.Add(new VendorStory("2", "Fix header", "open", []));

        await Refresh();

        var stories = (await Read()).Stories;
        stories.Count.ShouldBe(2);
        stories.ShouldContain(story => story.VendorId == "1" && story.Title == "Add login");
    }

    [Fact]
    public async Task Refresh_Should_BeIdempotentWhenNothingChanged()
    {
        await Configure();
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", ["bug"]));

        await Refresh();
        await Refresh();

        (await Read()).Stories.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Refresh_Should_UpdateInPlaceWhenAStoryIsRenamed()
    {
        await Configure();
        fixture.Vendor.Stories.Add(new VendorStory("1", "Old title", "open", []));
        await Refresh();

        fixture.Vendor.Stories.Clear();
        fixture.Vendor.Stories.Add(new VendorStory("1", "New title", "open", []));
        await Refresh();

        var stories = (await Read()).Stories;
        stories.Count.ShouldBe(1);
        stories[0].Title.ShouldBe("New title");
    }

    [Fact]
    public async Task Refresh_Should_RemoveStoriesTheVendorNoLongerReturns()
    {
        await Configure();
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", []));
        fixture.Vendor.Stories.Add(new VendorStory("2", "Fix header", "open", []));
        await Refresh();

        fixture.Vendor.Stories.RemoveAll(story => story.VendorId == "2");
        await Refresh();

        // The vendor is the source of truth (BR-008).
        (await Read())
            .Stories.Select(story => story.VendorId)
            .ShouldBe(["1"]);
    }

    [Fact]
    public async Task Refresh_Should_DegradeToStaleRatherThanEmptyWhenTheVendorFails()
    {
        await Configure();
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", []));
        await Refresh();

        fixture.Vendor.FetchError = BacklogErrors.VendorUnavailable("connection reset");
        var failed = await Refresh();

        failed.IsSuccessStatusCode.ShouldBeFalse();

        var backlog = await Read();
        // Previously mirrored Stories survive, and the failure is visible — so a client can tell
        // "we could not look" from "there is nothing here".
        backlog.Stories.Count.ShouldBe(1);
        backlog.Connector!.LastFailure.ShouldNotBeNull();
        backlog.Connector.LastFailureAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Refresh_Should_ClearAPreviousFailureOnceItSucceedsAgain()
    {
        await Configure();
        fixture.Vendor.FetchError = BacklogErrors.VendorUnavailable("boom");
        await Refresh();

        fixture.Vendor.FetchError = null;
        await Refresh();

        var backlog = await Read();
        backlog.Connector!.LastFailure.ShouldBeNull();
        backlog.Connector.LastSyncedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Read_Should_NameTheVendorRatherThanItsOrdinal()
    {
        await Configure();

        var backlog = await Read();

        // The vendor is projected as `Vendor.ToString()` from inside the EF query, so whether the
        // response says "GitHub" or "1" depends on the provider translating the enum rather than
        // casting it. It does today — and the page renders this value straight into a badge, so
        // the day a provider upgrade changes that, this should fail rather than the UI.
        backlog.Connector!.Vendor.ShouldBe(nameof(BacklogVendor.GitHub));
    }

    [Fact]
    public async Task Refresh_Should_RefuseAProjectWithNoConnector()
    {
        var response = await Refresh();

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Refresh_Should_MirrorTheDescriptionAndCountAnEditAsAChange()
    {
        await Configure();
        fixture.Vendor.Stories.Add(
            new VendorStory("1", "Add login", "open", [], "The original requirement")
        );
        await Refresh();

        var story = await _client.GetFromJsonAsync<StoryDetailResponse>(
            $"/api/projects/{_projectId}/backlog/stories/1"
        );
        story!.Body.ShouldBe("The original requirement");

        // An edited description is a change — the Agent would want to react to exactly this.
        fixture.Vendor.Stories[0] = new VendorStory("1", "Add login", "open", [], "Rewritten");
        var refreshed = await Refresh();
        var changes = await refreshed.Content.ReadFromJsonAsync<RefreshResponse>();
        changes!.Changes.ShouldBe(1);

        (
            await _client.GetFromJsonAsync<StoryDetailResponse>(
                $"/api/projects/{_projectId}/backlog/stories/1"
            )
        )!.Body.ShouldBe("Rewritten");
    }

    [Fact]
    public async Task GetStory_Should_RefuseAStoryTheMirrorDoesNotHold()
    {
        await Configure();

        var response = await _client.GetAsync($"/api/projects/{_projectId}/backlog/stories/nope");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Documents_Should_ListWhatTheLinkedChangeTouches()
    {
        await Configure();
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", [], null));
        await Refresh();

        fixture.Vendor.Change = new LinkedChange(
            42,
            "feat: add login",
            "https://example.invalid/pull/42",
            "abc123"
        );
        fixture.Vendor.Documents["docs/proposal.md"] = "# Proposal";
        fixture.Vendor.Documents["docs/design.md"] = "# Design";

        var documents = await _client.GetFromJsonAsync<DocumentsResponse>(
            $"/api/projects/{_projectId}/backlog/stories/1/documents"
        );

        documents!.Change.ShouldNotBeNull();
        documents.Change.Number.ShouldBe(42);
        documents.Documents.ShouldBe(["docs/design.md", "docs/proposal.md"]);
    }

    [Fact]
    public async Task Documents_Should_ReportNoLinkedChangeDistinctlyFromNoDocuments()
    {
        await Configure();
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", [], null));
        await Refresh();

        // No linked change at all.
        var none = await _client.GetFromJsonAsync<DocumentsResponse>(
            $"/api/projects/{_projectId}/backlog/stories/1/documents"
        );
        none!.Change.ShouldBeNull();
        none.Documents.ShouldBeEmpty();

        // A change that simply adds no markdown — a different fact (design D5).
        fixture.Vendor.Change = new LinkedChange(7, "chore", "https://example.invalid/7", "sha");
        var empty = await _client.GetFromJsonAsync<DocumentsResponse>(
            $"/api/projects/{_projectId}/backlog/stories/1/documents"
        );
        empty!.Change.ShouldNotBeNull();
        empty.Documents.ShouldBeEmpty();
    }

    [Fact]
    public async Task DocumentContent_Should_ReadAtTheChangesHeadRef()
    {
        await Configure();
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", [], null));
        await Refresh();

        fixture.Vendor.Change = new LinkedChange(
            42,
            "feat",
            "https://example.invalid/42",
            "head-sha"
        );
        fixture.Vendor.Documents["docs/proposal.md"] = "# Proposal\n\nThe text.";

        var content = await _client.GetFromJsonAsync<DocumentContentResponse>(
            $"/api/projects/{_projectId}/backlog/stories/1/documents/content?path=docs/proposal.md"
        );

        content!.Content.ShouldContain("The text.");
        // The head-ref contract, observed rather than assumed: this is what makes "the branch
        // moved on" correct by construction.
        content.HeadRef.ShouldBe("head-sha");
        fixture.Vendor.LastReadRef.ShouldBe("head-sha");
    }

    [Fact]
    public async Task DocumentContent_Should_SurfaceAReadFailureAsItself()
    {
        await Configure();
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", [], null));
        await Refresh();

        fixture.Vendor.Change = new LinkedChange(42, "feat", "https://example.invalid/42", "sha");
        fixture.Vendor.DocumentError = BacklogErrors.VendorUnavailable("connection reset");

        var response = await _client.GetAsync(
            $"/api/projects/{_projectId}/backlog/stories/1/documents/content?path=docs/x.md"
        );

        response.IsSuccessStatusCode.ShouldBeFalse();
        (await response.Content.ReadAsStringAsync()).ShouldContain("VendorUnavailable");
    }

    sealed record ChangeResponse(int Number, string Title, string Url, string HeadRef);

    sealed record DocumentsResponse(ChangeResponse? Change, IReadOnlyList<string> Documents);

    sealed record DocumentContentResponse(string Path, string HeadRef, string Content);

    sealed record StoryDetailResponse(
        string VendorId,
        string Title,
        string State,
        IReadOnlyList<string> Labels,
        string? Body
    );

    sealed record RefreshResponse(int Changes);

    sealed record ConnectorResponse(
        string Vendor,
        string Owner,
        string Repository,
        string SecretName,
        DateTimeOffset? LastSyncedAt,
        string? LastFailure,
        DateTimeOffset? LastFailureAt
    );

    sealed record StoryResponse(
        string VendorId,
        string Title,
        string State,
        IReadOnlyList<string> Labels
    );

    sealed record BacklogResponse(
        ConnectorResponse? Connector,
        IReadOnlyList<StoryResponse> Stories
    );
}
