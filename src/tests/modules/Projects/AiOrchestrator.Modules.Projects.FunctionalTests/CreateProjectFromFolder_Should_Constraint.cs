using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Projects.Persistence;
using ErrorOr;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// #347 / UC-003 — an Admin adds a Project by naming a folder on this machine, and everything else
/// follows from the folder.
/// </summary>
[Collection(ProjectsCollection.Name)]
public class CreateProjectFromFolder_Should_Constraint(ProjectsApiFixture fixture) : IAsyncLifetime
{
    const string Folder = "/Users/owner/code/portal";

    WebApplicationFactory<Program>? _selfHost;
    HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetDatabase();

        // Naming a folder exists only where the orchestrator runs on a machine its owner controls
        // (DEC-049), so the posture comes from a derived factory carrying
        // <c>Identity:Mode=LocalOwner</c> — the way the product composes it — rather than by faking
        // the habitat check. The shared fixture's own host has no mode, which is the deployment
        // posture, and there every call below is refused before the folder is ever inspected.
        _selfHost = fixture.WithWebHostBuilder(builder =>
            builder.UseSetting("Identity:Mode", "LocalOwner")
        );
        _client = _selfHost.CreateClient();
    }

    public Task DisposeAsync()
    {
        _selfHost?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ANamedFolder_Should_ConfigureTheConnectorInTheSameStep()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = "Phoenix", folder = Folder }
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<CreatedProject>();
        created.ShouldNotBeNull();
        created.Connector.ShouldNotBeNull();
        created.Connector.Configured.ShouldBeTrue();

        // Derived from `origin`, not typed by the Admin.
        created.Connector.Vendor.ShouldBe("GitHub");
        created.Connector.Owner.ShouldBe("acme");
        created.Connector.Repository.ShouldBe("portal");

        // And actually written, with the folder as its code source — a Project that reports
        // coordinates but configured nothing would be the failure this capability exists to remove.
        var written = fixture.Connectors.Created.ShouldHaveSingleItem();
        written.Owner.ShouldBe("acme");
        written.Repository.ShouldBe("portal");
        written.LocalPath.ShouldBe(Folder);
    }

    [Fact]
    public async Task AnAzureDevOpsFolder_Should_YieldTheThreeFieldsThatConnectorReads()
    {
        fixture.Folders.Inspection = StubLocalCodeWorkspace.Repository(
            "https://dev.azure.com/contoso/Platform/_git/api"
        );

        var response = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = "Platform", folder = Folder }
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var written = fixture.Connectors.Created.ShouldHaveSingleItem();
        written.Vendor.ShouldBe("AzureDevOps");
        written.Owner.ShouldBe("contoso");
        written.Repository.ShouldBe("Platform");
        written.CodeRepository.ShouldBe("api");
    }

    [Theory]
    [InlineData(false, true, "git@github.com:acme/portal.git", "notADirectory")]
    [InlineData(true, false, "git@github.com:acme/portal.git", "notAGitRepository")]
    [InlineData(true, true, null, "noOrigin")]
    [InlineData(true, true, "git@gitlab.com:acme/portal.git", "unknownVendor")]
    public async Task AFolderThatAnswersNothing_Should_NameTheCheckAndStillCreateTheProject(
        bool isDirectory,
        bool isRepository,
        string? origin,
        string expectedCheck
    )
    {
        fixture.Folders.Inspection = new PathInspection(
            isDirectory,
            isRepository,
            Branch: isRepository ? "main" : null,
            IsClean: isRepository ? true : null,
            OriginUrl: origin
        );

        var response = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"Project-{expectedCheck}", folder = Folder }
        );

        // The Project is still created — a folder that cannot answer is not a reason to refuse the
        // Project; the Admin types the coordinates instead.
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<CreatedProject>();
        created.ShouldNotBeNull();
        created.Connector.ShouldNotBeNull();
        created.Connector.Configured.ShouldBeFalse();

        // Which of the four, not a generic failure: the four have four different fixes.
        created.Connector.FailedCheck.ShouldBe(expectedCheck);
        created.Connector.Owner.ShouldBeNull();

        fixture.Connectors.Created.ShouldBeEmpty();
    }

    [Fact]
    public async Task ARefusedConnector_Should_LeaveNoProjectBehind()
    {
        fixture.Connectors.Refusal = Error.Validation(
            "Connector.HostCredentialUnavailable",
            "this machine holds no credential for github.com"
        );

        var response = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = "Orphan", folder = Folder }
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // The compensation: a Project with no Connector is the state this capability abolishes, so
        // a refused Connector must not leave one.
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ProjectsDbContext>();
        (await database.Projects.AnyAsync(project => project.Name == "Orphan")).ShouldBeFalse();
    }

    [Fact]
    public async Task ARelativeFolder_Should_BeRefusedBeforeAnythingIsCreated()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = "Relative", folder = "code/portal" }
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        fixture.Connectors.Created.ShouldBeEmpty();
    }

    [Fact]
    public async Task NoFolder_Should_CreateAProjectExactlyAsItAlwaysDid()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new { name = "Plain" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<CreatedProject>();
        created.ShouldNotBeNull();
        created.Connector.ShouldBeNull();
        fixture.Connectors.Created.ShouldBeEmpty();
    }

    sealed record CreatedProject(Guid Id, string Name, FolderOutcome? Connector);

    sealed record FolderOutcome(
        bool Configured,
        string? Vendor,
        string? Owner,
        string? Repository,
        string? CodeRepository,
        string? FailedCheck
    );
}
