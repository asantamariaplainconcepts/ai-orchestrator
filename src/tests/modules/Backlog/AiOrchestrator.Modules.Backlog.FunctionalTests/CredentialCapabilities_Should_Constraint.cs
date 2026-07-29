using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.Modules.Backlog.Domain;
using AiOrchestrator.Modules.Backlog.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.FunctionalTests;

/// <summary>
/// #132 — what "verified" means. What must hold: a credential that can read Stories but not files
/// is refused naming which, the refusal carries the vendor's reason, and the same probe answers
/// the on-demand test.
/// </summary>
[Collection(BacklogCollection.Name)]
public class CredentialCapabilities_Should_Constraint(BacklogApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    readonly Guid _projectId = Guid.CreateVersion7();

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Secrets.Reset();
        fixture.Caller.Reset();
        await fixture.ResetDatabase();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    Task<HttpResponseMessage> Configure() =>
        _client.PutAsJsonAsync(
            $"/api/projects/{_projectId}/connector",
            new
            {
                owner = "acme",
                repository = "portal",
                secretName = "acme-pat",
            }
        );

    async Task<bool> ConnectorExists()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<BacklogDbContext>();
        return await database.Connectors.AnyAsync(entity => entity.ProjectId == _projectId);
    }

    [Fact]
    public async Task ACredentialThatReadsStoriesButNotFiles_Should_BeRefusedNamingTheCapability()
    {
        fixture.Vendor.DocumentsRefusal = BacklogErrors.PermissionRefused(
            "reading the repository's files",
            "Resource not accessible by personal access token"
        );

        var response = await Configure();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();

        // Which read failed, and the vendor's own sentence — the two things that tell the Admin
        // what to grant. A generic "could not save" told them nothing (design D2).
        body.ShouldContain("reading the repository's files");
        body.ShouldContain("Resource not accessible by personal access token");

        (await ConnectorExists()).ShouldBeFalse();
    }

    [Fact]
    public async Task ACredentialThatReadsNeither_Should_BeRefusedOnTheStoriesFirst()
    {
        fixture.Vendor.StoriesRefusal = BacklogErrors.PermissionRefused(
            "reading the backlog's Stories",
            "no access"
        );
        fixture.Vendor.DocumentsRefusal = BacklogErrors.PermissionRefused(
            "reading the repository's files",
            "no access either"
        );

        var response = await Configure();

        // Stories first: a credential that cannot see the backlog at all makes the document
        // answer uninteresting.
        (await response.Content.ReadAsStringAsync()).ShouldContain("reading the backlog's Stories");
    }

    [Fact]
    public async Task ACredentialThatReadsBoth_Should_BeAccepted()
    {
        (await Configure()).EnsureSuccessStatusCode();

        (await ConnectorExists()).ShouldBeTrue();
    }

    [Fact]
    public async Task TheProbe_Should_AskForAPathThatIsNotThisFrameworksDocument()
    {
        (await Configure()).EnsureSuccessStatusCode();

        // Design D6: the path tests whether files are readable at all. Asking for
        // docs/process/definition-of-ready.md would read as though the verdict depended on this
        // repository's conventions.
        fixture.Vendor.ProbedDocumentPath.ShouldNotBeNull();
        fixture.Vendor.ProbedDocumentPath.ShouldNotContain("definition-of-ready");
    }

    [Fact]
    public async Task TheOnDemandTest_Should_ReportEveryCapability()
    {
        (await Configure()).EnsureSuccessStatusCode();

        var response = await _client.GetAsync($"/api/projects/{_projectId}/connector/test");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TestResponse>();
        result.ShouldNotBeNull();
        result.Satisfied.ShouldBeTrue();
        result.Capabilities.Count.ShouldBe(2);
        result.Capabilities.ShouldAllBe(capability => capability.Succeeded);
    }

    [Fact]
    public async Task TheOnDemandTest_Should_NameWhatWasRefusedAndChangeNothing()
    {
        (await Configure()).EnsureSuccessStatusCode();

        // Revoked after the Connector was stored — the case the button exists for.
        fixture.Vendor.DocumentsRefusal = BacklogErrors.PermissionRefused(
            "reading the repository's files",
            "Resource not accessible by personal access token"
        );

        var response = await _client.GetAsync($"/api/projects/{_projectId}/connector/test");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TestResponse>();
        result.ShouldNotBeNull();
        result.Satisfied.ShouldBeFalse();

        var refused = result.Capabilities.Single(capability => !capability.Succeeded);
        refused.Capability.ShouldBe("reading the repository's files");
        refused.Reason.ShouldNotBeNull();
        refused.Reason.ShouldContain("Resource not accessible");

        // A question, not a change: the Connector is exactly as it was (design D4).
        (await ConnectorExists()).ShouldBeTrue();
    }

    [Fact]
    public async Task TestingAProjectWithNoConnector_Should_SaySo()
    {
        var response = await _client.GetAsync(
            $"/api/projects/{Guid.CreateVersion7()}/connector/test"
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    sealed record CapabilityResponse(string Capability, bool Succeeded, string? Reason);

    sealed record TestResponse(bool Satisfied, IReadOnlyList<CapabilityResponse> Capabilities);
}
