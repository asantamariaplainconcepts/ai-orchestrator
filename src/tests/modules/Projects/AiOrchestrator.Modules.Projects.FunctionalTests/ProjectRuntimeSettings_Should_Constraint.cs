using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// project-runtimes (#244) — the Project's default runtime and per-runtime credential names.
/// The write is a full replace (the Automation update's own rule), the read echoes exactly what
/// is stored, and the payload carries names only — never values (BR-010). The Member-side
/// refusals live with the other role assertions in
/// <see cref="ProjectRoleAssignment_Should_Constraint"/>.
/// </summary>
[Collection(ProjectsCollection.Name)]
public class ProjectRuntimeSettings_Should_Constraint(ProjectsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();

    Guid _projectId;

    public async Task InitializeAsync()
    {
        await fixture.ResetDatabase();

        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"Runtimes {Guid.CreateVersion7()}" }
        );
        created.EnsureSuccessStatusCode();
        _projectId = (await created.Content.ReadFromJsonAsync<ProjectResponse>())!.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    Task<HttpResponseMessage> Put(string? defaultRuntime, Dictionary<string, string> names) =>
        _client.PutAsJsonAsync(
            $"/api/projects/{_projectId}/runtimes",
            new { defaultRuntime, credentialNames = names }
        );

    Task<SettingsResponse?> Get() =>
        _client.GetFromJsonAsync<SettingsResponse>($"/api/projects/{_projectId}/runtimes");

    [Fact]
    public async Task ANewProject_Should_StartOnTheDeploymentDefault()
    {
        var settings = await Get();

        // Null is the answer, not an absence: no default chosen, no names stored.
        settings!.DefaultRuntime.ShouldBeNull();
        settings.CredentialNames.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheSettings_Should_RoundTrip()
    {
        var saved = await Put(
            "OpenCode",
            new Dictionary<string, string>
            {
                ["OpenCode"] = "acme-opencode-key",
                ["ClaudeCodeHeadless"] = "acme-anthropic-key",
            }
        );
        saved.EnsureSuccessStatusCode();

        var settings = await Get();
        settings!.DefaultRuntime.ShouldBe("OpenCode");
        settings.CredentialNames.Count.ShouldBe(2);
        settings.CredentialNames["OpenCode"].ShouldBe("acme-opencode-key");
        settings.CredentialNames["ClaudeCodeHeadless"].ShouldBe("acme-anthropic-key");
    }

    [Fact]
    public async Task TheWrite_Should_BeAFullReplace()
    {
        (
            await Put(
                "OpenCode",
                new Dictionary<string, string> { ["OpenCode"] = "acme-opencode-key" }
            )
        ).EnsureSuccessStatusCode();

        // Saving with the default cleared and no names must remove both — a merge here would
        // make a credential name undeletable from the form.
        (await Put(null, [])).EnsureSuccessStatusCode();

        var settings = await Get();
        settings!.DefaultRuntime.ShouldBeNull();
        settings.CredentialNames.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnUnknownRuntime_Should_BeRefusedAsMalformed()
    {
        (await Put("Copilot", [])).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (
            await Put(null, new Dictionary<string, string> { ["Copilot"] = "a-name" })
        ).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AnUnknownProject_Should_Be404()
    {
        var response = await _client.GetAsync($"/api/projects/{Guid.CreateVersion7()}/runtimes");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ARuntimelessAutomation_Should_RoundTripItsNull()
    {
        // The "Project default" option (#244, design D4): stored as null, listed as null —
        // resolution to a concrete runtime happens at execution time, never at rest.
        var created = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "ai:implement",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = (string?)null,
                promptPath = "story.md",
                requiresApproval = false,
                timeoutMinutes = (int?)null,
            }
        );
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        var automations = await _client.GetFromJsonAsync<List<AutomationResponse>>(
            $"/api/projects/{_projectId}/automations"
        );
        automations!.Single().Runtime.ShouldBeNull();
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record SettingsResponse(
        string? DefaultRuntime,
        Dictionary<string, string> CredentialNames
    );

    sealed record AutomationResponse(Guid Id, string? Runtime);
}
