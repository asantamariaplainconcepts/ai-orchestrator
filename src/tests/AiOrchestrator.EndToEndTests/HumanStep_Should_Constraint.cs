using System.Text.Json;
using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// #137, rewritten for #310 — requiring a person, asserted where it is a fact: in the stored Automation.
/// <para>
/// <b>Rewritten against the board rather than removed.</b> The capability this suite covered survives
/// the canvas's deletion; only its home moved. It used to press "Require a person here" on the canvas's
/// connector, which cleared the preceding step's output label. The same sentence is now the boundary's
/// clear control on the Backlog board, and it clears the claimed transition — the field the hand-off
/// moved into. A test asserting the canvas would have been asserting a screen that no longer exists;
/// deleting it would have dropped a capability's only coverage. So it drives the new control and
/// asserts the new field.
/// </para>
/// <para>
/// Driven through the explicit control rather than a drag, and that is not a shortcut. Playwright cannot
/// perform an HTML5 drag — #110 recorded the same thing — which is precisely why dragging is sugar and
/// the control is the semantics. Both call one function, so this covers the logic either gesture reaches
/// (AC 12).
/// </para>
/// <para>
/// The claim worth asserting is that the two human waits stay different things (BR-006 against BR-007):
/// clearing the claim makes the boundary a person's turn and leaves <c>requiresApproval</c> alone, and
/// leaves the marks alone too — which is the transition/mark split holding under a gesture. Conflating
/// them was the first draft of #137, and it would have drawn one picture for two run-time behaviours.
/// </para>
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class HumanStep_Should_Constraint(AppHostFixture fixture)
{
    [Fact]
    public async Task RequiringAPerson_Should_ClearTheClaimAndLeaveApprovalAlone()
    {
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, $"Human step — {Guid.NewGuid():N}");

        // Two Automations and one claim: grill moves a Story on to `ready-for-proposal`, so the
        // lifecycle is those two stages and the boundary between them is claimed. The mark is there so
        // the gesture has something adjacent it must not touch.
        await CreateAutomation(
            page,
            projectId,
            "ai:grill",
            toStage: "ready-for-proposal",
            marks: ["needs-design"]
        );
        await CreateAutomation(page, projectId, "ready-for-proposal");

        await Board(page, projectId);

        var control = page.GetByRole(AriaRole.Button, new() { Name = "Require a person here" });
        await control.First.WaitForAsync(new() { Timeout = 30_000 });
        await control.First.ClickAsync();

        // Asserted against the API rather than the picture: the claim is what is stored, so its absence
        // is the fact. Polled to a deadline rather than read once — the click starts a mutation and
        // returns, and a single read races it about a third of the time, which is exactly the flake
        // #107 taught this repository to write out of its tests rather than live with.
        var stored = await Eventually(
            () => Automations(page, projectId),
            automations =>
                automations.Single(automation => automation.TriggerLabel == "ai:grill").ToStage
                    is null
        );
        var grill = stored.Single(automation => automation.TriggerLabel == "ai:grill");

        // The boundary is now unclaimed, which is what "a person carries it across" is stored as.
        grill.ToStage.ShouldBeNull();

        // The mark survives. Since #310 a mark is not a hand-off, so clearing the hand-off must not
        // take one with it — the whole point of splitting the two.
        grill.OutputLabels.ShouldBe(["needs-design"]);

        // The other wait, untouched. BR-007's two-phase Run is a different thing from a person carrying
        // work across a boundary, and the board must keep them distinguishable.
        grill.RequiresApproval.ShouldBeFalse();
        stored
            .Single(automation => automation.TriggerLabel == "ready-for-proposal")
            .RequiresApproval.ShouldBeFalse();

        // And the boundary now says so, as a fact about who acts: no validation error, no
        // "incomplete configuration" marker and no elapsed time (BR-006, AC 3).
        var boundary = page.Locator("[data-boundary='ready-for-proposal']");
        await boundary.First.WaitForAsync(new() { Timeout = 15_000 });
        await Assertions
            .Expect(boundary.First)
            .ToContainTextAsync("A person", new() { Timeout = 15_000 });
        (await page.Locator("[data-boundary] [role=alert]").CountAsync()).ShouldBe(0);
    }

    /// <summary>Opens the project's Backlog board. No Connector and no Story is needed since #310:
    /// the columns are the project's lifecycle, which is configuration rather than vendor
    /// contents.</summary>
    async Task Board(IPage page, Guid projectId)
    {
        // Through the remembered preference rather than the view toggle, whose own label flips on click
        // — the element Playwright resolved is detached before it finishes checking the click is
        // stable, which is a race about the toggle in a test about the boundary.
        await page.AddInitScriptAsync("window.localStorage.setItem('aio:backlog-view', 'board')");
        // ?tab=operate explicitly: a project with no Connector lands on Settings, because configuring
        // is the job on day one (ProjectScreen's derived landing tab).
        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=operate");
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
        string? toStage = null,
        string[]? marks = null
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
                    action = "RepositoryPrompt",
                    runtime = "ClaudeCodeHeadless",
                    promptPath = "story.md",
                    requiresApproval = false,
                    outputLabels = marks ?? [],
                    toStage,
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
        bool RequiresApproval,
        string? ToStage
    );
}
