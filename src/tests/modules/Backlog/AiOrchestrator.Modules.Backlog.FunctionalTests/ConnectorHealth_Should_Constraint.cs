using System.Net.Http.Json;
using AiOrchestrator.Modules.Backlog.Connectors;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.FunctionalTests;

/// <summary>
/// #97 — the ambient health read. Four states must be distinguishable from this one response,
/// and nothing in it may carry configuration a list does not need (no secret name even).
/// </summary>
[Collection(BacklogCollection.Name)]
public class ConnectorHealth_Should_Constraint(BacklogApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        await fixture.ResetDatabase();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task<Guid> ProjectWithConnector()
    {
        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        created.EnsureSuccessStatusCode();
        var projectId = (await created.Content.ReadFromJsonAsync<ProjectResponse>())!.Id;

        (
            await _client.PutAsJsonAsync(
                $"/api/projects/{projectId}/connector",
                new
                {
                    owner = "acme",
                    repository = "portal",
                    secretName = "acme-pat",
                }
            )
        ).EnsureSuccessStatusCode();

        return projectId;
    }

    [Fact]
    public async Task TheStates_Should_BeDistinguishableFromOneResponse()
    {
        // Never-synced: configured, no refresh yet.
        var neverSynced = await ProjectWithConnector();

        // Healthy: configured and refreshed against a working vendor.
        var healthy = await ProjectWithConnector();
        fixture.Vendor.Stories.Add(new VendorStory("1", "A story", "open", [], null));
        (
            await _client.PostAsync($"/api/projects/{healthy}/backlog/refresh", null)
        ).EnsureSuccessStatusCode();

        // Failing: the vendor starts refusing, and the next refresh records it.
        var failing = await ProjectWithConnector();
        fixture.Vendor.FetchError = Domain.BacklogErrors.VendorUnavailable("rate limited");
        await _client.PostAsync($"/api/projects/{failing}/backlog/refresh", null);
        fixture.Vendor.FetchError = null;

        var connectors = await _client.GetFromJsonAsync<IReadOnlyList<ConnectorRow>>(
            "/api/connectors"
        );

        // Not-configured is the absence: three rows for four projects' worth of states.
        connectors!.Count.ShouldBe(3);
        connectors.Single(row => row.ProjectId == neverSynced).LastSyncedAt.ShouldBeNull();
        connectors.Single(row => row.ProjectId == healthy).LastSyncedAt.ShouldNotBeNull();
        connectors.Single(row => row.ProjectId == healthy).LastFailure.ShouldBeNull();
        var failed = connectors.Single(row => row.ProjectId == failing);
        failed.LastFailure.ShouldNotBeNull();
        failed.LastFailure.ShouldContain("rate limited");
        connectors.ShouldAllBe(row => row.Vendor == "GitHub");
    }

    [Fact]
    public async Task Recovery_Should_ShowOnTheNextOrdinaryRefresh()
    {
        var projectId = await ProjectWithConnector();
        fixture.Vendor.FetchError = Domain.BacklogErrors.VendorUnavailable("down");
        await _client.PostAsync($"/api/projects/{projectId}/backlog/refresh", null);

        fixture.Vendor.FetchError = null;
        fixture.Vendor.Stories.Add(new VendorStory("1", "A story", "open", [], null));
        (
            await _client.PostAsync($"/api/projects/{projectId}/backlog/refresh", null)
        ).EnsureSuccessStatusCode();

        var connectors = await _client.GetFromJsonAsync<IReadOnlyList<ConnectorRow>>(
            "/api/connectors"
        );

        connectors!.Single().LastFailure.ShouldBeNull();
    }

    [Fact]
    public async Task TheResponse_Should_CarryNoCredentialAndNoSecretName()
    {
        await ProjectWithConnector();

        var raw = await _client.GetStringAsync("/api/connectors");

        // BR-010 and then some: the health list needs no configuration at all, so even the
        // secret NAME stays out — the narrowest read that serves the purpose.
        raw.ShouldNotContain("stub-token");
        raw.ShouldNotContain("acme-pat");
        raw.ShouldNotContain("secretName");
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record ConnectorRow(
        Guid ProjectId,
        string Vendor,
        DateTimeOffset? LastSyncedAt,
        string? LastFailure,
        DateTimeOffset? LastFailureAt
    );
}
