using System.Net.Http.Json;
using AiOrchestrator.Modules.Backlog.Connectors;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.FunctionalTests;

/// <summary>
/// ADR-0002 requires at least one test that exercises concurrency, because a sequential suite is
/// structurally blind to lifetime and sharing bugs — the class of defect that cost a full change
/// to find last time.
/// <para>
/// Reconciliation is upsert-plus-delete against a uniqueness constraint, so parallel refreshes of
/// the same Project are exactly where duplicates or lost updates would appear.
/// </para>
/// </summary>
[Collection(BacklogCollection.Name)]
public class ConcurrentRefresh_Should_Constraint(BacklogApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    readonly Guid _projectId = Guid.CreateVersion7();

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        await fixture.ResetDatabase();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ParallelRefreshes_Should_NotDuplicateStories()
    {
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

        for (var i = 1; i <= 5; i++)
        {
            fixture.Vendor.Stories.Add(
                new VendorStory(
                    i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    $"Story {i}",
                    "open",
                    []
                )
            );
        }

        // Eight refreshes at once against one Project.
        var refreshes = await Task.WhenAll(
            Enumerable
                .Range(0, 8)
                .Select(_ =>
                    _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", content: null)
                )
        );

        // Losing a race is acceptable — a concurrent write may legitimately conflict. Producing
        // duplicates is not, and neither is a 500. On failure, surface the server's own words:
        // a bare status code turns a five-minute diagnosis into an hour of guessing.
        var failures = refreshes.Where(response => (int)response.StatusCode >= 500).ToList();
        if (failures.Count > 0)
        {
            var body = await failures[0].Content.ReadAsStringAsync();
            failures.Count.ShouldBe(
                0,
                $"a concurrent refresh returned {failures[0].StatusCode}:\n{body}"
            );
        }

        var backlog = await _client.GetFromJsonAsync<BacklogResponse>(
            $"/api/projects/{_projectId}/backlog"
        );
        backlog!.Stories.Count.ShouldBe(5);
        backlog.Stories.Select(story => story.VendorId).Distinct().Count().ShouldBe(5);
    }

    sealed record StoryResponse(
        string VendorId,
        string Title,
        string State,
        IReadOnlyList<string> Labels
    );

    sealed record BacklogResponse(object? Connector, IReadOnlyList<StoryResponse> Stories);
}
