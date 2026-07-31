using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// #137 — placing the human review, asserted where it is a fact: in the stored Automations.
/// <para>
/// Driven through the explicit control rather than the drag, and that is not a shortcut. Playwright
/// cannot perform an HTML5 drag — #110 recorded the same thing — which is precisely why dragging is
/// sugar and the control is the semantics. Both call one function, so this covers the logic either
/// gesture reaches.
/// </para>
/// <para>
/// The claim worth asserting is that the two human waits stay different things: placing the block
/// clears the preceding step's output label and leaves approval alone. Conflating them was the first
/// draft of this change, and it would have drawn one picture for two run-time behaviours.
/// </para>
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class HumanStep_Should_Constraint(AppHostFixture fixture)
{
    [Fact]
    public async Task PlacingTheHumanReview_Should_BreakTheChainAndLeaveApprovalAlone()
    {
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Human step — the chain breaks");

        // Two Automations, chained: grill hands work to propose by writing its trigger label.
        var upstream = await CreateAutomation(
            page,
            projectId,
            "ai:grill",
            "RepositoryPrompt",
            "ready-for-proposal"
        );
        await CreateAutomation(page, projectId, "ready-for-proposal", "RepositoryPrompt", null);

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=automations");

        // The control that breaks the connection is the one a keyboard reaches and the one the
        // block's drop shares.
        var breakChain = page.GetByRole(AriaRole.Button, new() { Name = "Require a person here" });
        await breakChain.WaitForAsync(new() { Timeout = 30_000 });
        await breakChain.ClickAsync();

        // Asserted against the API rather than the picture: the chain is a label agreement, so the
        // absence of the label is the fact.
        //
        // Polled to a deadline rather than read once. The click starts a mutation and returns; a
        // single read races it and fails about a third of the time, which is exactly the flake
        // #107 taught this repository to write out of its tests rather than live with.
        var stored = await Eventually(
            () => Automations(page, projectId),
            automations =>
                automations
                    .Single(automation => automation.TriggerLabel == "ai:grill")
                    .OutputLabels.Count == 0
        );
        var grill = stored.Single(automation => automation.TriggerLabel == "ai:grill");

        // Empty, not "the set lost one member of several": placing a human here removes the edge
        // this gap represents and leaves any others alone (#165), and this Automation had one.
        grill.OutputLabels.ShouldBeEmpty();

        // The other wait, untouched. BR-007's two-phase Run is a different thing from reviewing what
        // the previous step produced, and the workflow must keep them distinguishable.
        grill.RequiresApproval.ShouldBeFalse();
        stored
            .Single(automation => automation.TriggerLabel == "ready-for-proposal")
            .RequiresApproval.ShouldBeFalse();

        _ = upstream;
    }

    /// <summary>
    /// Reads until the condition holds or the deadline passes, then hands back the last reading so
    /// the caller's own assertions produce the failure message.
    /// </summary>
    static async Task<T> Eventually<T>(Func<Task<T>> read, Func<T, bool> until)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(15);
        var latest = await read();

        while (!until(latest) && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(250);
            latest = await read();
        }

        return latest;
    }

    async Task<Guid> CreateProject(IPage page, string name)
    {
        var response = await page.APIRequest.PostAsync(
            $"{fixture.ServerBaseUrl}api/projects",
            new APIRequestContextOptions { DataObject = new { name } }
        );
        response.Status.ShouldBe(201, await response.TextAsync());

        using var document = JsonDocument.Parse(await response.TextAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    async Task<Guid> CreateAutomation(
        IPage page,
        Guid projectId,
        string triggerLabel,
        string action,
        string? outputLabel
    )
    {
        var response = await page.APIRequest.PostAsync(
            $"{fixture.ServerBaseUrl}api/projects/{projectId}/automations",
            new APIRequestContextOptions
            {
                DataObject = new
                {
                    triggerLabel,
                    triggerState = (string?)null,
                    action,
                    runtime = "ClaudeCodeHeadless",
                    requiresApproval = false,
                    outputLabels = outputLabel is null ? Array.Empty<string>() : [outputLabel],
                },
            }
        );
        response.Status.ShouldBe(201, await response.TextAsync());

        using var document = JsonDocument.Parse(await response.TextAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    async Task<IReadOnlyList<StoredAutomation>> Automations(IPage page, Guid projectId)
    {
        var response = await page.APIRequest.GetAsync(
            $"{fixture.ServerBaseUrl}api/projects/{projectId}/automations"
        );
        response.Status.ShouldBe(200, await response.TextAsync());

        return JsonSerializer.Deserialize<List<StoredAutomation>>(
            await response.TextAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        )!;
    }

    sealed record StoredAutomation(
        Guid Id,
        string TriggerLabel,
        IReadOnlyList<string> OutputLabels,
        bool RequiresApproval
    );
}
