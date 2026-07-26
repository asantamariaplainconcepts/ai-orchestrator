using System.Net.Http.Json;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Domain;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.FunctionalTests;

/// <summary>
/// UC-008: the write goes to the vendor first, the mirror follows through the ordinary sync,
/// and a vendor refusal leaves the mirror untouched — stale-not-lying by construction.
/// </summary>
[Collection(BacklogCollection.Name)]
public class WriteStoryLabel_Should_Constraint(BacklogApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    readonly Guid _projectId = Guid.CreateVersion7();

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        await fixture.ResetDatabase();

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
    }

    public Task DisposeAsync() => Task.CompletedTask;

    Task<HttpResponseMessage> Apply(string vendorStoryId, string label) =>
        _client.PutAsync(
            $"/api/projects/{_projectId}/backlog/stories/{vendorStoryId}/labels/{label}",
            content: null
        );

    Task<HttpResponseMessage> Remove(string vendorStoryId, string label) =>
        _client.DeleteAsync(
            $"/api/projects/{_projectId}/backlog/stories/{vendorStoryId}/labels/{label}"
        );

    async Task<List<StoryResponse>> Stories() =>
        (
            await _client.GetFromJsonAsync<BacklogResponse>($"/api/projects/{_projectId}/backlog")
        )!.Stories;

    [Fact]
    public async Task Apply_Should_WriteToTheVendorAndThenTheMirror()
    {
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", []));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);

        (await Apply("1", "ai:implement")).EnsureSuccessStatusCode();

        // The vendor got the write…
        fixture.Vendor.Stories[0].Labels.ShouldContain("ai:implement");
        // …and the mirror agrees because it was re-synchronised, not patched.
        (await Stories())
            .Single()
            .Labels.ShouldContain("ai:implement");
    }

    [Fact]
    public async Task Remove_Should_WriteBackTheSameWay()
    {
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", ["ai:implement"]));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);

        (await Remove("1", "ai:implement")).EnsureSuccessStatusCode();

        fixture.Vendor.Stories[0].Labels.ShouldBeEmpty();
        (await Stories()).Single().Labels.ShouldBeEmpty();
    }

    [Fact]
    public async Task AVendorRefusal_Should_LeaveTheMirrorUntouched()
    {
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", []));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);

        fixture.Vendor.WriteError = BacklogErrors.VendorUnavailable("connection reset");
        var response = await Apply("1", "ai:implement");

        response.IsSuccessStatusCode.ShouldBeFalse();
        (await Stories()).Single().Labels.ShouldBeEmpty();
    }

    [Fact]
    public async Task BothOperations_Should_BeIdempotent()
    {
        fixture.Vendor.Stories.Add(new VendorStory("1", "Add login", "open", []));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);

        (await Apply("1", "ai:implement")).EnsureSuccessStatusCode();
        (await Apply("1", "ai:implement")).EnsureSuccessStatusCode();
        (await Remove("1", "never-there")).EnsureSuccessStatusCode();

        var labels = (await Stories()).Single().Labels;
        labels.ShouldBe(["ai:implement"]);
    }

    [Fact]
    public async Task AProjectWithoutAConnector_Should_BeRefused()
    {
        var orphan = Guid.CreateVersion7();
        var response = await _client.PutAsync(
            $"/api/projects/{orphan}/backlog/stories/1/labels/x",
            content: null
        );

        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    sealed record StoryResponse(
        string VendorId,
        string Title,
        string State,
        IReadOnlyList<string> Labels
    );

    sealed record BacklogResponse(object? Connector, List<StoryResponse> Stories);
}
