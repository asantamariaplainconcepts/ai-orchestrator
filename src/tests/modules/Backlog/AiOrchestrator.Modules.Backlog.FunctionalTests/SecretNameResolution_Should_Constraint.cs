using System.Net.Http.Json;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.FunctionalTests;

/// <summary>
/// Design review 5d — the live "does it resolve?" beside the secret-name field. Two properties:
/// the answer comes through the same seam every real resolution uses, and it is one boolean —
/// the value never travels, whatever the verdict.
/// </summary>
[Collection(BacklogCollection.Name)]
public class SecretNameResolution_Should_Constraint(BacklogApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    readonly Guid _projectId = Guid.CreateVersion7();

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Secrets.Reset();
        await fixture.ResetDatabase();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task<CheckResponse> Check(string name) =>
        (
            await _client.GetFromJsonAsync<CheckResponse>(
                $"/api/projects/{_projectId}/connector/secret-resolves?name={Uri.EscapeDataString(name)}"
            )
        )!;

    [Fact]
    public async Task ANameTheHabitatResolves_Should_SaySo()
    {
        (await Check("acme-pat")).Resolves.ShouldBeTrue();
    }

    [Fact]
    public async Task ANameThatResolvesToNothing_Should_SayNotYet()
    {
        // The one name the fixture's vault deliberately misses — the typo-or-not-restarted case
        // this endpoint exists to catch before the Connector fails on it.
        (await Check("missing-secret")).Resolves.ShouldBeFalse();
    }

    [Fact]
    public async Task ABlankName_Should_NotResolve()
    {
        var response = await _client.GetAsync(
            $"/api/projects/{_projectId}/connector/secret-resolves?name="
        );

        response.EnsureSuccessStatusCode();
        (await response.Content.ReadFromJsonAsync<CheckResponse>())!.Resolves.ShouldBeFalse();
    }

    [Fact]
    public async Task TheAnswer_Should_CarryNoValue()
    {
        var response = await _client.GetAsync(
            $"/api/projects/{_projectId}/connector/secret-resolves?name=acme-pat"
        );

        response.EnsureSuccessStatusCode();
        // The stub vault resolves every known name to this value; the response must not.
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("stub-token");
    }

    sealed record CheckResponse(bool Resolves);
}
