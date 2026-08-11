using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// #310 task 3.5 / design D5 — BR-003 needs <b>no fourth enforcement home</b> under the claim model,
/// confirmed by exercise rather than by reading the three that exist.
/// <para>
/// The rule was already "at most one enabled Automation per trigger", and a claim's from-stage
/// <i>is</i> its trigger label (design D2) — so "one claimant per transition" is the same sentence the
/// expression index (<c>20260729150023_UniqueAutomationTrigger.cs:25-30</c>) and
/// <c>OverlapGuard.Check</c> already enforce, over the same column. Adding a guard for the claim would
/// have been a second implementation of one rule, which is the failure <c>OverlapGuard.cs:9-13</c>
/// records for BR-003.
/// </para>
/// <para>
/// What is asserted here is what that claim depends on: the refusal folds case (DEC-056), it
/// <b>names</b> the Automation already claiming the from-stage, it fires on the edit path as well as
/// the create path, and the refused Automation is left exactly as it was. The concurrent case — two
/// saves racing for one trigger, refused by the index rather than by the guard — is already asserted
/// in <c>TriggerIdentity_Should_Constraint</c> and is deliberately not restated.
/// </para>
/// </summary>
[Collection(ProjectsCollection.Name)]
public class ClaimedFromStage_Should_Constraint(ProjectsApiFixture fixture) : IAsyncLifetime
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

    Task<HttpResponseMessage> Claim(string fromStage, string toStage) =>
        _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = fromStage,
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "story.md",
                requiresApproval = false,
                toStage,
            }
        );

    Task<HttpResponseMessage> Reclaim(Guid id, string fromStage, string toStage) =>
        _client.PutAsJsonAsync(
            $"/api/projects/{_projectId}/automations/{id}",
            new
            {
                triggerLabel = fromStage,
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "story.md",
                requiresApproval = false,
                toStage,
            }
        );

    async Task<IReadOnlyList<AutomationResponse>> List() =>
        (
            await _client.GetFromJsonAsync<List<AutomationResponse>>(
                $"/api/projects/{_projectId}/automations"
            )
        )!;

    [Fact]
    public async Task ASecondEnabledClaimantOfAFromStage_Should_BeRefusedAndNameTheFirst()
    {
        (await Claim("ai:grill", "ai:propose")).EnsureSuccessStatusCode();

        // Differing only in case, which is the whole point: the vendor treats these as one label, so
        // one from-stage cannot have two enabled claimants however it is spelled.
        var second = await Claim("AI:GRILL", "ai:review");

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var body = await second.Content.ReadAsStringAsync();
        body.ShouldContain("TriggerOverlaps");
        // Named, not merely refused (OverlapGuard.cs:78-81). "Invalid" would leave an Admin to find
        // the other claimant themselves, which on a board of ten boundaries is the whole difficulty.
        body.ShouldContain("ai:grill");

        var stored = await List();
        stored.Count.ShouldBe(1);
        stored[0].ToStage.ShouldBe("ai:propose");
    }

    [Fact]
    public async Task MovingAClaimOntoAnotherClaimantsFromStage_Should_BeRefusedAndChangeNothing()
    {
        (await Claim("ai:grill", "ai:propose")).EnsureSuccessStatusCode();
        var moving = await Claim("ai:review", "ai:sync");
        moving.EnsureSuccessStatusCode();
        var movingId = (await moving.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;

        // The edit path, which is the one an arrangement change takes (AC 5): the same guard, the
        // third of its three callers, with the subject excluded from its own comparison.
        var refused = await Reclaim(movingId, "ai:grill", "ai:propose");

        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // And nothing moved. A refusal that had already written half the edit would be worse than
        // one that never fired.
        var stored = await List();
        var subject = stored.Single(automation => automation.Id == movingId);
        subject.TriggerLabel.ShouldBe("ai:review");
        subject.ToStage.ShouldBe("ai:sync");
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id, string TriggerLabel, string? ToStage);
}
