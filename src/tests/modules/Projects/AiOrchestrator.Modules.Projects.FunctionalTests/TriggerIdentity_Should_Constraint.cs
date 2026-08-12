using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// #147 / DEC-056 — what makes two triggers the same one. What must hold: the vendor's comparison
/// (case-insensitive), an exact duplicate refused whether or not either side is enabled, subsumption
/// still enabled-only, and the schema enforcing it so a concurrent pair cannot both land.
/// </summary>
[Collection(ProjectsCollection.Name)]
public class TriggerIdentity_Should_Constraint(ProjectsApiFixture fixture) : IAsyncLifetime
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

    Task<HttpResponseMessage> Create(string triggerLabel, string? triggerState = null) =>
        _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel,
                triggerState,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "story.md",
            }
        );

    // /disable, not /enabled with a body — read from UpdateAutomation's routes after guessing.
    async Task Disable(Guid automationId) =>
        (
            await _client.PostAsync(
                $"/api/projects/{_projectId}/automations/{automationId}/disable",
                null
            )
        ).EnsureSuccessStatusCode();

    async Task<Guid> IdOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;

    [Fact]
    public async Task ADifferentlyCasedTrigger_Should_BeRefusedAsTheSameOne()
    {
        (await Create("AI:Implement")).EnsureSuccessStatusCode();

        var response = await Create("ai:implement");

        // The vendor treats those as one label, so this product must too (DEC-056).
        // Conflict, not BadRequest: TriggerOverlaps is Error.Conflict, which is what BR-003's
        // refusal has always been. Read from ProjectErrors rather than assumed a second time.
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ADisabledExactDuplicate_Should_StillBeRefused()
    {
        var first = await Create("ai:refine", "open");
        first.EnsureSuccessStatusCode();
        await Disable(await IdOf(first));

        // Two rows carrying one trigger are one trigger, whatever Enabled says. Before #147 this
        // was allowed and the conflict surfaced at enable time, to somebody who did not cause it.
        (await Create("ai:refine", "open")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ADisabledBroadSibling_Should_NotSubsumeANarrowEnabledOne()
    {
        var broad = await Create("ai:estimate");
        broad.EnsureSuccessStatusCode();
        await Disable(await IdOf(broad));

        // Subsumption is about matching, and a disabled Automation matches nothing — so BR-003's
        // meaning is unchanged by the stricter exact-duplicate rule.
        (await Create("ai:estimate", "open")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DifferentStatesOnOneLabel_Should_StillBothSave()
    {
        (await Create("ai:transition", "open")).EnsureSuccessStatusCode();

        // No Story carries two states at once, so neither can match both.
        (await Create("ai:transition", "closed")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task TwoIdenticalSavesAtOnce_Should_LeaveOneAutomationAndOneRefusal()
    {
        // The race the index exists for: both callers pass the in-memory guard, and only one write
        // can win. Before #147 both landed, producing the twins BR-003 forbids.
        var attempts = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => Create("ai:grill", "open"))
        );

        attempts.Count(response => response.IsSuccessStatusCode).ShouldBe(1);

        // And every loser got the rule's own refusal, never an internal error.
        foreach (var refused in attempts.Where(response => !response.IsSuccessStatusCode))
        {
            refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }

        var stored = await _client.GetFromJsonAsync<List<AutomationResponse>>(
            $"/api/projects/{_projectId}/automations"
        );
        stored!.Count(automation => automation.TriggerLabel == "ai:grill").ShouldBe(1);
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id, string TriggerLabel);
}
