using System.Text.Json;
using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// #310 — the board writes an Automation, so what it writes is asserted here.
/// <para>
/// This case exists because of a defect read from the code and then <b>reproduced</b> rather than
/// argued about (ADR-0005): the board's own write control restated eight fields into the wholesale
/// PUT and omitted <c>model</c>, so pressing it reverted a chosen model to the deployment's — #291's
/// failure recurring on the surface that writes least often and is therefore noticed last. The
/// claimed transition would have been the second field lost the same way. Run against the code before
/// the fix, the model assertion below is red; the fix is routing that call site through
/// <c>requestFor</c>, ADR-0019's one builder.
/// </para>
/// <para>
/// Driven through the explicit control, never a drag. Playwright cannot perform an HTML5 drag (#110),
/// which is exactly why every arrangement change is offered by a control that shares the drop's
/// function — the control is what puts the logic under test at all (AC 12).
/// </para>
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class BoardArrangement_Should_Constraint(AppHostFixture fixture)
{
    [Fact]
    public async Task TheBoardsOwnWrite_Should_LeaveEveryFieldItDoesNotShow()
    {
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, $"Board — fields survive {Guid.NewGuid():N}");

        // A chosen model: a field the board never renders, and therefore the field an inline request
        // forgets. The claim is what the gesture is *about*; the model is what it must not touch.
        await CreateAutomation(
            page,
            projectId,
            "ai:grill",
            toStage: "ready-for-proposal",
            model: "claude-sonnet-4-5"
        );
        await CreateAutomation(page, projectId, "ready-for-proposal");

        await Board(page, projectId);

        var control = page.GetByRole(AriaRole.Button, new() { Name = "Require a person here" });
        await control.First.WaitForAsync(new() { Timeout = 30_000 });
        await control.First.ClickAsync();

        // Polled rather than read once: the click starts a mutation and returns, and a single read
        // races it — the flake #107 taught this repository to write out of its tests.
        var stored = await Eventually(
            () => Automations(page, projectId),
            automations =>
                automations.Single(automation => automation.TriggerLabel == "ai:grill").ToStage
                    is null
        );
        var grill = stored.Single(automation => automation.TriggerLabel == "ai:grill");

        // The gesture's own outcome, so a green model assertion cannot come from nothing happening.
        grill.ToStage.ShouldBeNull();

        // And the field the board cannot see is exactly as it was. This is the assertion that was red
        // before the fix, driven then through the control this one replaced.
        grill.Model.ShouldBe("claude-sonnet-4-5");
    }

    [Fact]
    public async Task EveryStage_Should_BeAColumnInTheStoredOrder()
    {
        // AC 1: a lifecycle of three stages with one claim renders three columns in the stored order,
        // and the last is not omitted for having no Automation claiming the transition into it — a stage
        // is never pruned. AC 2 and AC 3 ride along, because they are facts about the same picture.
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, $"Board — every stage {Guid.NewGuid():N}");

        // Two claims build three stages. The second Automation is then deleted, which leaves its
        // to-stage in the lifecycle with nobody claiming the transition into it.
        await CreateAutomation(page, projectId, "st:one", toStage: "st:two");
        var second = await CreateAutomation(page, projectId, "st:two", toStage: "st:three");
        await Delete(page, projectId, second);

        await Board(page, projectId);

        foreach (var stage in new[] { "st:one", "st:two", "st:three" })
        {
            await page.Locator($"section[data-stage='{stage}']")
                .First.WaitForAsync(new() { Timeout = 30_000 });
        }

        (await Columns(page)).ShouldBe(["st:one", "st:two", "st:three"]);

        // AC 2: the Automation renders on the boundary between its two stages, and on no other.
        // Located by the claimant marker rather than by text, because every boundary's assign control
        // names every Automation — a text assertion would have passed on the option list.
        (
            await page.Locator("[data-boundary='st:two'] [data-claimant='st:one']").CountAsync()
        ).ShouldBe(1);
        (await page.Locator("[data-boundary='st:three'] [data-claimant]").CountAsync()).ShouldBe(0);

        // AC 3: the unclaimed boundary states who acts. No validation error, no "incomplete
        // configuration" marker, and no elapsed time anywhere on a boundary (BR-006).
        await Assertions
            .Expect(page.Locator("[data-boundary='st:three']"))
            .ToContainTextAsync("A person");
        (await page.Locator("[data-boundary] [role=alert]").CountAsync()).ShouldBe(0);

        // AC 8: the end of the flow says the flow ends, and asserts nothing about who acts next.
        await Assertions
            .Expect(page.Locator("[data-flow-end]"))
            .ToContainTextAsync("The flow ends here");
    }

    [Fact]
    public async Task AStep_Should_BePlaceableFirstThroughTheBoundaryControl()
    {
        // AC 4: assigning an Automation to the boundary into the first stage makes its own trigger label
        // the board's new first column, and leaves the order of the existing stages alone. Through the
        // explicit control, because that is the path a test can drive and a keyboard user has (AC 12).
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, $"Board — placed first {Guid.NewGuid():N}");

        await CreateAutomation(page, projectId, "pf:one", toStage: "pf:two");
        // Claims nothing yet, and its trigger is not a stage — so it is nowhere on the flow.
        await CreateAutomation(page, projectId, "pf:zero");

        await Board(page, projectId);
        await page.Locator("section[data-stage='pf:two']")
            .First.WaitForAsync(new() { Timeout = 30_000 });
        (await Columns(page)).ShouldBe(["pf:one", "pf:two"]);

        await AssignAt(page, "pf:one", "pf:zero");

        // The new stage lands immediately before the one it moves work into, and the two that were
        // already there keep their order.
        await Eventually(() => Columns(page), columns => columns.Length == 3);
        (await Columns(page)).ShouldBe(["pf:zero", "pf:one", "pf:two"]);

        var stored = await Automations(page, projectId);
        stored
            .Single(automation => automation.TriggerLabel == "pf:zero")
            .ToStage.ShouldBe("pf:one");
        // And nobody else's claim moved (AC 5's second clause, which holds for every assignment).
        stored
            .Single(automation => automation.TriggerLabel == "pf:one")
            .ToStage.ShouldBe("pf:two");
    }

    [Fact]
    public async Task MovingAClaim_Should_LeaveEveryOtherClaimAlone()
    {
        // AC 5: assigning an Automation to a later boundary through the explicit control moves it there
        // and nowhere else, the boundary it left renders per AC 3, and no other Automation's claimed
        // transition changed.
        //
        // What "moving" stores is worth stating, because it is not only the to-stage: a claim's
        // from-stage IS the Automation's trigger label (design D2), so a step moved to the boundary
        // between `mv:two` and `mv:three` now fires on `mv:two`. Both fields travel through one
        // function, which is what makes this expressible at all.
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, $"Board — reorder {Guid.NewGuid():N}");

        await CreateAutomation(page, projectId, "mv:one", toStage: "mv:two");
        // A throwaway third claim to extend the lifecycle, then deleted so its from-stage is free for
        // the move below — BR-003 permits only one enabled Automation per from-stage (AC 6).
        var scaffold = await CreateAutomation(page, projectId, "mv:two", toStage: "mv:three");
        await Delete(page, projectId, scaffold);
        // A bystander, so "no other claim changed" has something to be true of.
        await CreateAutomation(page, projectId, "mv:other", toStage: "mv:elsewhere");

        await Board(page, projectId);
        await page.Locator("section[data-stage='mv:three']")
            .First.WaitForAsync(new() { Timeout = 30_000 });

        await AssignAt(page, "mv:three", "mv:one");

        var stored = await Eventually(
            () => Automations(page, projectId),
            automations => automations.Any(automation => automation.ToStage == "mv:three")
        );

        // It moved, and it moved as a whole: it fires at `mv:two` now and hands on to `mv:three`.
        var moved = stored.Single(automation => automation.ToStage == "mv:three");
        moved.TriggerLabel.ShouldBe("mv:two");

        // The bystander is exactly as it was.
        stored
            .Single(automation => automation.TriggerLabel == "mv:other")
            .ToStage.ShouldBe("mv:elsewhere");

        // And the boundary it left is a person's turn, with no fault reported (AC 3).
        await Assertions
            .Expect(page.Locator("[data-boundary='mv:two']"))
            .ToContainTextAsync("A person", new() { Timeout = 15_000 });
        (await page.Locator("[data-boundary] [role=alert]").CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task ASecondClaimant_Should_BeRefusedByTheApisOwnReason()
    {
        // AC 6: BR-003 refuses a second enabled claimant of one transition, the refusal names the
        // Automation already claiming it, and neither Automation changed. Asserted through the board's
        // own control, because the sentence a reader gets is the API's — a generic line would throw the
        // name away, which is the whole content of the refusal.
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, $"Board — one claimant {Guid.NewGuid():N}");

        await CreateAutomation(page, projectId, "dup:one", toStage: "dup:two");
        await CreateAutomation(page, projectId, "dup:rival");

        await Board(page, projectId);
        await page.Locator("section[data-stage='dup:two']")
            .First.WaitForAsync(new() { Timeout = 30_000 });

        // The rival is offered rather than hidden: a named refusal beats an unexplained absence.
        await AssignAt(page, "dup:two", "dup:rival");

        await Assertions
            .Expect(page.GetByRole(AriaRole.Alert).First)
            .ToContainTextAsync("dup:one", new() { Timeout = 15_000 });

        // Neither changed.
        var stored = await Automations(page, projectId);
        stored
            .Single(automation => automation.TriggerLabel == "dup:one")
            .ToStage.ShouldBe("dup:two");
        stored.Single(automation => automation.TriggerLabel == "dup:rival").ToStage.ShouldBeNull();
    }

    [Fact]
    public async Task ThePhonePager_Should_OfferTheBoundaryControlToo()
    {
        // AC 12 at the width where the capability used to be missing. This is what survives of
        // #232's suite: the canvas hid its drag entirely below xl, so a phone could not rearrange a
        // pipeline at all, and asserting only at desktop is exactly what let that stay missing.
        //
        // Two columns never share a phone screen, so the boundary travels with the column it leads
        // into — "the transition into what you are looking at". Asserted as "the control is reachable
        // and visible at 375px" rather than by performing a drag, which Playwright cannot do (#110).
        var page = await fixture.Browser.NewPageAsync();
        await page.SetViewportSizeAsync(375, 900);
        var projectId = await CreateProject(page, $"Board — phone {Guid.NewGuid():N}");

        await CreateAutomation(page, projectId, "ph:one", toStage: "ph:two");
        await CreateAutomation(page, projectId, "ph:two");

        await Board(page, projectId);

        // The pager starts on Untouched, so paging to the stage is the gesture that brings its
        // boundary on screen — which is the reading this layout is built around.
        var pager = page.GetByRole(AriaRole.Button, new() { Name = "ph:two", Exact = false });
        await pager.First.WaitForAsync(new() { Timeout = 30_000 });
        await pager.First.ClickAsync();

        var control = page.GetByLabel("Move an Automation here… ph:two");
        await control.First.WaitForAsync(new() { Timeout = 15_000 });
        (await control.First.IsVisibleAsync()).ShouldBeTrue();
    }

    /// <summary>The boundary's explicit control: choose an Automation for the transition into a stage.</summary>
    static async Task AssignAt(IPage page, string toStage, string triggerLabel)
    {
        var select = page.GetByLabel($"Move an Automation here… {toStage}");
        await select.First.WaitForAsync(new() { Timeout = 30_000 });
        await select.First.SelectOptionAsync(new SelectOptionValue { Label = triggerLabel });
    }

    /// <summary>The rendered columns, read off the board rather than assumed.</summary>
    static Task<string[]> Columns(IPage page) =>
        page.EvaluateAsync<string[]>(
            "() => [...document.querySelectorAll('section[data-stage]')].map((node) => node.getAttribute('data-stage'))"
        );

    async Task Delete(IPage page, Guid projectId, Guid automationId)
    {
        var response = await page.APIRequest.DeleteAsync(
            $"{fixture.ServerBaseUrl}api/projects/{projectId}/automations/{automationId}"
        );
        response.Ok.ShouldBeTrue(await response.TextAsync());
    }

    /// <summary>Opens the project's Backlog board. No Connector and no Story is needed since #310:
    /// the columns are the project's lifecycle, which is configuration rather than vendor
    /// contents.</summary>
    async Task Board(IPage page, Guid projectId)
    {
        // Through the remembered preference rather than the toggle. The toggle's own label flips on
        // click, so the element Playwright resolved is detached before it finishes checking the click
        // is stable — a race about the toggle, in a test about what the board writes. Seeding the
        // preference the product itself reads keeps the assertion on its own subject.
        await page.AddInitScriptAsync("window.localStorage.setItem('aio:backlog-view', 'board')");
        // ?tab=operate explicitly: a project with no Connector lands on Settings, because
        // configuring is the job on day one (ProjectScreen's derived landing tab). A deep link is
        // the user saying where to be, and this test is about the board.
        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=operate");
    }

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

        if (response.Status is not (200 or 201))
        {
            throw new InvalidOperationException(
                $"Could not seed a project: {response.Status} {await response.TextAsync()}\n\n"
                    + fixture.ServerLogTail(lines: 100)
            );
        }

        using var document = JsonDocument.Parse(await response.TextAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    async Task<Guid> CreateAutomation(
        IPage page,
        Guid projectId,
        string triggerLabel,
        string? model = null,
        string? toStage = null
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
                    outputLabels = Array.Empty<string>(),
                    model,
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
        string? Model,
        string? ToStage
    );
}
