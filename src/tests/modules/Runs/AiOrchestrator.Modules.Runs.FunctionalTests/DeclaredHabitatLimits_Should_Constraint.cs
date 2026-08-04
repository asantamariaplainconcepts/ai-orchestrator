using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #247 — a habitat whose composition declares the Local locus unavailable refuses by name at
/// both doors: the <c>LocalFolder</c> save and the Local-locus Run resolution. The declared
/// sentence travels verbatim — never a path error from inside a container. The compose self-host
/// is the habitat this models: still self-host (LocalOwner), but the Server is a container and
/// the operator's folders are not its to see.
/// </summary>
[Collection(RunsCollection.Name)]
public class DeclaredHabitatLimits_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    const string Declared =
        "the orchestrator runs in a container here, and a folder on this machine is not visible to it";

    WebApplicationFactory<Program>? _declaring;
    WebApplicationFactory<Program>? _devLoop;
    HttpClient _client = null!;
    Guid _projectId;
    string _repoPath = string.Empty;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Agent.Reset();
        fixture.Workspace.Reset();
        await fixture.ResetDatabase();
        await fixture.ResetQueue();

        // Composed exactly as the product composes it: the same self-host key the dev loop
        // carries, plus the declaration the generated compose sets — never a faked check.
        _declaring = fixture.WithWebHostBuilder(builder =>
            builder
                .UseSetting("Identity:Mode", "LocalOwner")
                .UseSetting("Habitat:LocalFolderUnavailableReason", Declared)
        );
        _client = _declaring.CreateClient();

        // The dev loop flavour, for arranging the pre-existing-Connector case: self-host with
        // nothing declared, where a LocalFolder save legitimately succeeds.
        _devLoop = fixture.WithWebHostBuilder(builder =>
            builder.UseSetting("Identity:Mode", "LocalOwner")
        );

        _repoPath = Directory.CreateTempSubdirectory("declared-limit-").FullName;

        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        created.EnsureSuccessStatusCode();
        _projectId = JsonDocument
            .Parse(await created.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id")
            .GetGuid();
    }

    public Task DisposeAsync()
    {
        _declaring?.Dispose();
        _devLoop?.Dispose();
        if (Directory.Exists(_repoPath))
        {
            Directory.Delete(_repoPath, recursive: true);
        }
        return Task.CompletedTask;
    }

    Task<HttpResponseMessage> ConfigureLocal(HttpClient client) =>
        client.PutAsJsonAsync(
            $"/api/projects/{_projectId}/connector",
            new
            {
                owner = "acme",
                repository = "portal",
                secretName = "acme-pat",
                codeSource = "localFolder",
                localPath = _repoPath,
            }
        );

    [Fact]
    public async Task ALocalFolderSave_Should_BeRefusedWithTheDeclaredSentence()
    {
        var response = await ConfigureLocal(_client);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        // The declared sentence verbatim — the same one the capabilities read carries — and
        // never the container path error this refusal exists to preempt.
        (await response.Content.ReadAsStringAsync()).ShouldContain(Declared);

        // Nothing stored — asked of the store itself, not of a read model with a default shape.
        await using var scope = _declaring!.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<BacklogDbContext>();
        (
            await database.Connectors.AnyAsync(entity => entity.ProjectId == _projectId)
        ).ShouldBeFalse();
    }

    [Fact]
    public async Task ARepositorySave_Should_BeUntouchedByTheDeclaration()
    {
        // The declaration is about one locus, not about self-host: everything else works.
        var response = await _client.PutAsJsonAsync(
            $"/api/projects/{_projectId}/connector",
            new
            {
                owner = "acme",
                repository = "portal",
                secretName = "acme-pat",
            }
        );

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task APreExistingLocalConnector_Should_NotProduceALocalRun()
    {
        // Arranged through the dev loop, where the save is legitimate — the case the second
        // door exists for: the Connector predates the declaration (or arrived around the
        // portal), and the refusal must still land before any container path is touched.
        await Git("init", "--initial-branch=main");
        await File.WriteAllTextAsync(Path.Combine(_repoPath, "readme.md"), "hello");
        await Git("add", "--all");
        await Git(
            "-c",
            "user.name=Owner",
            "-c",
            "user.email=owner@example.invalid",
            "commit",
            "-m",
            "seed"
        );

        using var devLoopClient = _devLoop!.CreateClient();
        (await ConfigureLocal(devLoopClient)).EnsureSuccessStatusCode();

        var automation = await devLoopClient.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "ai:refine",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "refine.md",
                requiresApproval = false,
            }
        );
        automation.EnsureSuccessStatusCode();
        var automationId = JsonDocument
            .Parse(await automation.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id")
            .GetGuid();

        fixture.Vendor.Stories.Add(new VendorStory("1", "Story", "open", [], "B."));
        await devLoopClient.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 1);

        var run = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "1", automationId }
        );

        run.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await run.Content.ReadAsStringAsync()).ShouldContain(Declared);

        // Refused, not failed: no Run row exists — the BR-016 pre-write pattern.
        var runs = await _client.GetFromJsonAsync<JsonElement>($"/api/projects/{_projectId}/runs");
        runs.GetArrayLength().ShouldBe(0);
    }

    async Task<string> Git(params string[] arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = _repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(startInfo)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }
}
