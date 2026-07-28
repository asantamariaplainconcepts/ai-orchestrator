using System.Net.Http.Json;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// UC-005 in one action. The behaviours that matter are what happens the *second* time and when
/// somebody has already claimed a trigger — a set-up button that only works on a pristine project
/// is a button people learn not to press.
/// </summary>
[Collection(ProjectsCollection.Name)]
public class AutomationDefaults_Should_Constraint(ProjectsApiFixture fixture) : IAsyncLifetime
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

    async Task<DefaultsResponse> ApplyDefaults()
    {
        var response = await _client.PostAsync(
            $"/api/projects/{_projectId}/automations/defaults",
            content: null
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DefaultsResponse>())!;
    }

    async Task<IReadOnlyList<AutomationResponse>> Automations() =>
        (
            await _client.GetFromJsonAsync<IReadOnlyList<AutomationResponse>>(
                $"/api/projects/{_projectId}/automations"
            )
        )!;

    [Fact]
    public async Task AnUnconfiguredProject_Should_GetOneAutomationPerCatalogueAction()
    {
        var result = await ApplyDefaults();

        result.Created.Count.ShouldBe(6);
        result.Skipped.ShouldBeEmpty();

        var automations = await Automations();
        automations
            .Select(automation => automation.TriggerLabel)
            .OrderBy(label => label)
            .ShouldBe([
                "ai:estimate",
                "ai:grill",
                "ai:implement",
                "ai:refine",
                "ai:transition",
                "ready-for-proposal",
            ]);
        // One per catalogue action: the set advertising a smaller catalogue than the product has
        // is exactly the drift this asserts against.
        automations
            .Select(automation => automation.Action)
            .OrderBy(action => action)
            .ShouldBe([
                "Estimate",
                "GrillToReady",
                "ImplementToPullRequest",
                "ProposeSpec",
                "RefineOrComment",
                "TransitionState",
            ]);
    }

    [Fact]
    public async Task OnlyTheActionThatWritesCode_Should_RequireApproval()
    {
        await ApplyDefaults();

        var automations = await Automations();

        automations
            .Single(automation => automation.Action == "ImplementToPullRequest")
            .RequiresApproval.ShouldBeTrue();
        automations
            .Where(automation => automation.Action != "ImplementToPullRequest")
            .ShouldAllBe(automation => !automation.RequiresApproval);
    }

    [Fact]
    public async Task TheDefaultRuntime_Should_CostNothingToRun()
    {
        // A one-click action that quietly starts spending on a paid runtime would be a bad
        // default, however convenient (design D5).
        await ApplyDefaults();

        (await Automations()).ShouldAllBe(automation => automation.Runtime == "OpenCode");
    }

    [Fact]
    public async Task ASecondApplication_Should_ChangeNothingAndSaySo()
    {
        await ApplyDefaults();

        var again = await ApplyDefaults();

        // Not a 409: the state the Admin wanted is the state that exists, and reporting that as
        // an error would make the action look broken the second time (design D2).
        again.Created.ShouldBeEmpty();
        again.Skipped.Count.ShouldBe(6);
        (await Automations()).Count.ShouldBe(6);
    }

    [Fact]
    public async Task ATriggerAlreadyTaken_Should_SkipOneAndCreateTheRest()
    {
        var existing = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "ai:refine",
                triggerState = (string?)null,
                action = "TransitionState",
                runtime = "ClaudeCodeHeadless",
                requiresApproval = false,
            }
        );
        existing.EnsureSuccessStatusCode();

        var result = await ApplyDefaults();

        result.Created.Count.ShouldBe(5);
        result.Skipped.Single().TriggerLabel.ShouldBe("ai:refine");

        // And the Admin's own Automation is untouched — the defaults defer to what is already
        // there rather than correcting it.
        var kept = (await Automations()).Single(automation =>
            automation.TriggerLabel == "ai:refine"
        );
        kept.Action.ShouldBe("TransitionState");
        kept.Runtime.ShouldBe("ClaudeCodeHeadless");
    }

    [Fact]
    public async Task AProjectWithNoConnector_Should_StillGetItsAutomations()
    {
        var result = await ApplyDefaults();

        result.Created.Count.ShouldBe(6);
        // Nothing to ensure labels against, and the response says so rather than implying the
        // labels are ready to pick at a vendor that was never configured.
        result.LabelNote.ShouldNotBeNull();
    }

    [Fact]
    public async Task AProjectSeededBeforeTheSetGrew_Should_ReceiveOnlyTheAdditions()
    {
        // The catalogue grew twice after the button shipped. A project seeded from the old set
        // must get the new actions from the same button — no migration, no version marker, just
        // BR-003 making a second press additive (design D3).
        foreach (
            var (trigger, action) in new[]
            {
                ("ai:implement", "ImplementToPullRequest"),
                ("ai:refine", "RefineOrComment"),
                ("ai:estimate", "Estimate"),
                ("ai:transition", "TransitionState"),
            }
        )
        {
            (
                await _client.PostAsJsonAsync(
                    $"/api/projects/{_projectId}/automations",
                    new
                    {
                        triggerLabel = trigger,
                        triggerState = (string?)null,
                        action,
                        runtime = "OpenCode",
                        requiresApproval = false,
                    }
                )
            ).EnsureSuccessStatusCode();
        }

        var result = await ApplyDefaults();

        result.Created.Count.ShouldBe(2);
        result
            .Created.Select(automation => automation.TriggerLabel)
            .OrderBy(label => label)
            .ShouldBe(["ai:grill", "ready-for-proposal"]);
        result.Skipped.Count.ShouldBe(4);
    }

    [Fact]
    public async Task TheSeededSet_Should_WireGrillIntoPropose()
    {
        await ApplyDefaults();

        var automations = await Automations();

        // The grill's ready label defaults to "ready-for-proposal"; seeding propose to listen on
        // exactly that is what makes the button hand over a working pipeline rather than six
        // unrelated triggers (design D1).
        automations
            .Single(automation => automation.Action == "ProposeSpec")
            .TriggerLabel.ShouldBe("ready-for-proposal");
        automations
            .Single(automation => automation.Action == "GrillToReady")
            .TriggerLabel.ShouldBe("ai:grill");
    }

    [Fact]
    public async Task AnUnknownProject_Should_BeRefused()
    {
        var response = await _client.PostAsync(
            $"/api/projects/{Guid.CreateVersion7()}/automations/defaults",
            content: null
        );

        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(
        Guid Id,
        string TriggerLabel,
        string? TriggerState,
        string Action,
        string Runtime,
        bool RequiresApproval
    );

    sealed record SkippedResponse(string TriggerLabel, string Reason);

    sealed record DefaultsResponse(
        IReadOnlyList<AutomationResponse> Created,
        IReadOnlyList<SkippedResponse> Skipped,
        string? LabelNote
    );
}
