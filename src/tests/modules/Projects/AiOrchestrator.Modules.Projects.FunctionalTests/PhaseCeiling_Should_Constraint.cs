using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// #144 / DEC-054 — the phase timeout is bounded, and the bound is what makes BR-005 keepable. The
/// validator allowed 720 minutes against a platform budget of ten, which is how dev came to kill
/// every implement Run over ten minutes.
/// </summary>
[Collection(ProjectsCollection.Name)]
public class PhaseCeiling_Should_Constraint(ProjectsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;

    public async Task InitializeAsync()
    {
        await fixture.ResetDatabase();

        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        created.EnsureSuccessStatusCode();
        _projectId = (await created.Content.ReadFromJsonAsync<ProjectResponse>())!.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    Task<HttpResponseMessage> Create(int? timeoutMinutes) =>
        _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = $"ai:t-{Guid.NewGuid():N}",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "story.md",
                timeoutMinutes,
            }
        );

    [Fact]
    public async Task ATimeoutAboveTheCeiling_Should_BeRefusedNamingIt()
    {
        var response = await Create(PhaseBudget.MaximumMinutes + 1);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain(PhaseBudget.MaximumMinutes.ToString());
        // The refusal explains itself, because "60" without the reason reads as an arbitrary cap.
        body.ShouldContain("provably sufficient");
    }

    [Fact]
    public async Task ATimeoutAtTheCeiling_Should_BeAccepted()
    {
        (await Create(PhaseBudget.MaximumMinutes)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task TheOldUpperBound_Should_NoLongerBeAccepted()
    {
        // 720 was the previous limit — twelve hours against a ten-minute platform budget.
        (await Create(720)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NoTimeout_Should_TakeTheDefault()
    {
        var response = await Create(null);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<AutomationTimeout>();
        created.ShouldNotBeNull();
        created.TimeoutMinutes.ShouldBe((int)PhaseBudget.Default.TotalMinutes);
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationTimeout(Guid Id, int TimeoutMinutes);
}
