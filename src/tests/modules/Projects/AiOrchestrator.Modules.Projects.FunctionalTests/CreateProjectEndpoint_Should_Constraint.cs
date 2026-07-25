using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

[Collection(ProjectsCollection.Name)]
public class CreateProjectEndpoint_Should_Constraint(ProjectsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();

    public Task InitializeAsync() => fixture.ResetDatabase();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Post_Should_CreateProjectAndReturnIt()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new { name = "Phoenix" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        created.ShouldNotBeNull();
        created.Name.ShouldBe("Phoenix");
        created.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Post_Should_RejectEmptyNameWithProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new { name = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Post_Should_RejectDuplicateNameAsConflict()
    {
        await _client.PostAsJsonAsync("/api/projects", new { name = "Duplicate" });

        var response = await _client.PostAsJsonAsync("/api/projects", new { name = "Duplicate" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Get_Should_ListCreatedProjects()
    {
        await _client.PostAsJsonAsync("/api/projects", new { name = "Listed" });

        var projects = await _client.GetFromJsonAsync<ProjectResponse[]>("/api/projects");

        projects.ShouldNotBeNull();
        projects.ShouldContain(project => project.Name == "Listed");
    }

    sealed record ProjectResponse(Guid Id, string Name);
}
