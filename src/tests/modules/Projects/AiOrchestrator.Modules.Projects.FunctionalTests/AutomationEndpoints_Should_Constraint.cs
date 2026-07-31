using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// Configure → reject → list, against real containers. The rejection cases carry the weight:
/// creating one Automation proves plumbing, while BR-003 is only proven by the saves that fail.
/// </summary>
[Collection(ProjectsCollection.Name)]
public class AutomationEndpoints_Should_Constraint(ProjectsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();

    Guid _projectId;

    public async Task InitializeAsync()
    {
        await fixture.ResetDatabase();

        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"Automations {Guid.CreateVersion7()}" }
        );
        created.EnsureSuccessStatusCode();
        var project = await created.Content.ReadFromJsonAsync<ProjectResponse>();
        _projectId = project!.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    Task<HttpResponseMessage> Create(
        string label,
        string? state = null,
        string action = "RepositoryPrompt"
    ) =>
        _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = label,
                triggerState = state,
                action,
                runtime = "ClaudeCodeHeadless",
                requiresApproval = false,
                timeoutMinutes = (int?)null,
            }
        );

    async Task<IReadOnlyList<AutomationResponse>> List() =>
        (
            await _client.GetFromJsonAsync<List<AutomationResponse>>(
                $"/api/projects/{_projectId}/automations"
            )
        )!;

    Task<HttpResponseMessage> Update(
        Guid id,
        string label,
        string? state = null,
        string action = "RepositoryPrompt"
    ) =>
        _client.PutAsJsonAsync(
            $"/api/projects/{_projectId}/automations/{id}",
            new
            {
                triggerLabel = label,
                triggerState = state,
                action,
                runtime = "ClaudeCodeHeadless",
                requiresApproval = false,
                timeoutMinutes = (int?)null,
            }
        );

    Task<HttpResponseMessage> SetEnabled(Guid id, bool enabled) =>
        _client.PostAsync(
            $"/api/projects/{_projectId}/automations/{id}/{(enabled ? "enable" : "disable")}",
            content: null
        );

    async Task<Guid> CreateId(string label, string? state = null)
    {
        var response = await Create(label, state);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;
    }

    [Fact]
    public async Task Update_Should_ApplyTheChange()
    {
        var id = await CreateId("ai:implement");

        (await Update(id, "ai:build", "open")).EnsureSuccessStatusCode();

        var automation = (await List()).Single();
        automation.TriggerLabel.ShouldBe("ai:build");
        automation.TriggerState.ShouldBe("open");
    }

    [Fact]
    public async Task Update_Should_NotCompareTheAutomationWithItself()
    {
        var id = await CreateId("ai:implement");

        // Editing only the action leaves the trigger identical — refusing this as an overlap
        // would make every Automation uneditable after creation.
        (
            await Update(id, "ai:implement", state: null, action: "Estimate")
        ).EnsureSuccessStatusCode();

        (await List()).Single().Action.ShouldBe("Estimate");
    }

    [Fact]
    public async Task Update_Should_BeRefusedWhenItWouldOverlapAnother()
    {
        var first = await CreateId("ai:implement", "open");
        await CreateId("ai:review");

        // BR-003's subsumption case, reached by editing rather than creating: a state-less
        // trigger matches everything the state-specific one does.
        var response = await Update(first, "ai:review");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        // Nothing changed: the stored Automation still has its original trigger.
        (await List())
            .Single(automation => automation.Id == first)
            .TriggerLabel.ShouldBe("ai:implement");
    }

    [Fact]
    public async Task Disabling_Should_NeverBeRefused_AndEnablingShould()
    {
        var first = await CreateId("ai:implement", "open");

        (await SetEnabled(first, enabled: false)).EnsureSuccessStatusCode();
        (await List()).Single(a => a.Id == first).Enabled.ShouldBeFalse();

        // While it was off, a trigger that subsumes it appeared — so re-enabling must refuse.
        await CreateId("ai:implement");

        var response = await SetEnabled(first, enabled: true);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await List()).Single(a => a.Id == first).Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task AnUnknownAutomation_Should_Be404()
    {
        (await Update(Guid.CreateVersion7(), "ai:implement")).StatusCode.ShouldBe(
            HttpStatusCode.NotFound
        );
        (await SetEnabled(Guid.CreateVersion7(), enabled: false)).StatusCode.ShouldBe(
            HttpStatusCode.NotFound
        );
    }

    [Fact]
    public async Task Create_Should_StoreTheAutomationAndDefaultItsTimeout()
    {
        (await Create("ai:implement", "open")).StatusCode.ShouldBe(HttpStatusCode.Created);

        var automations = await List();
        automations.Count.ShouldBe(1);
        automations[0].TriggerLabel.ShouldBe("ai:implement");
        // BR-005's default, applied when the caller omits it.
        automations[0].TimeoutMinutes.ShouldBe(30);
        automations[0].Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_Should_ReturnEnumNamesRatherThanOrdinals()
    {
        await Create("ai:implement", "open");

        // #7 shipped a projection whose enum was translated to SQL and came back as "0". The
        // check is on the raw body: a typed deserialisation would hide it.
        var raw = await _client.GetStringAsync($"/api/projects/{_projectId}/automations");
        raw.ShouldContain("RepositoryPrompt");
        raw.ShouldContain("ClaudeCodeHeadless");
    }

    [Fact]
    public async Task Create_Should_RejectAnExactDuplicateTrigger()
    {
        await Create("ai:implement", "open");

        var second = await Create("ai:implement", "open");

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await second.Content.ReadAsStringAsync();
        body.ShouldContain("TriggerOverlaps");
        // The refusal must name what it collided with — "invalid" leaves the Admin guessing.
        body.ShouldContain("ai:implement");
    }

    [Fact]
    public async Task Create_Should_AllowTheSameLabelInDifferentStates()
    {
        (await Create("ai:implement", "open")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await Create("ai:implement", "closed")).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await List()).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Create_Should_RejectABroadTriggerAfterANarrowOne()
    {
        await Create("ai:implement", "open");

        // Any-state after specific: a Story that is open and labelled would match both.
        (await Create("ai:implement")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_Should_RejectANarrowTriggerAfterABroadOne()
    {
        await Create("ai:implement");

        // The mirror image. Both directions are asserted because the rule is symmetric and it
        // would be easy to implement only the half that the first test exercises.
        (await Create("ai:implement", "open")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_Should_RefuseAnUnknownAction()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "ai:implement",
                triggerState = (string?)null,
                action = "TakeOverTheWorld",
                runtime = "ClaudeCodeHeadless",
                requiresApproval = false,
                timeoutMinutes = (int?)null,
            }
        );

        // A malformed request, not a business conflict — the distinction the two error channels
        // exist for.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_Should_RefuseAProjectThatDoesNotExist()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{Guid.CreateVersion7()}/automations",
            new
            {
                triggerLabel = "ai:implement",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                requiresApproval = false,
                timeoutMinutes = (int?)null,
            }
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Automations_Should_NeverCarryACredential()
    {
        await Create("ai:implement", "open");

        // BR-010 in its weakest-looking place: an Automation names a runtime, and a runtime is
        // exactly the sort of thing a future change might be tempted to give a token to.
        var raw = await _client.GetStringAsync($"/api/projects/{_projectId}/automations");
        foreach (var forbidden in new[] { "token", "secret", "password", "apiKey" })
        {
            raw.ShouldNotContain(forbidden, Case.Insensitive);
        }
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(
        Guid Id,
        string TriggerLabel,
        string? TriggerState,
        string Action,
        string Runtime,
        bool RequiresApproval,
        int TimeoutMinutes,
        bool Enabled
    );
}
