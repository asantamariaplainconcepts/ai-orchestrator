using System.Net;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// Host behaviour, exercised through the shared fixture: the SPA is served same-origin and the
/// reserved API prefixes are never swallowed by the fallback. The full-browser version of this
/// lives in the E2E lane; this keeps the routing contract covered on every test run.
/// </summary>
[Collection(ProjectsCollection.Name)]
public class SpaFallback_Should_Constraint(ProjectsApiFixture fixture)
{
    readonly HttpClient _client = fixture.CreateClient();

    [Fact]
    public async Task Host_Should_ServeTheSpaShellAtTheRoot()
    {
        var response = await _client.GetAsync("/");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }

    [Fact]
    public async Task Host_Should_FallBackToTheSpaForClientRoutes()
    {
        // A path only the client router knows about must still return the shell, not a 404.
        var response = await _client.GetAsync("/projects/some-client-route");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }

    [Fact]
    public async Task Host_Should_NotLetTheSpaFallbackSwallowReservedPrefixes()
    {
        var health = await _client.GetAsync("/api/health");

        health.StatusCode.ShouldBe(HttpStatusCode.OK);
        health.Content.Headers.ContentType?.MediaType.ShouldNotBe("text/html");
    }
}
