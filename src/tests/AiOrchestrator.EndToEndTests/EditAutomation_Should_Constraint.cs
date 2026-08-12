using System.Text.Json;
using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// #151 — editing an Automation from the portal. What must hold: the form opens on what is stored, a
/// save is the full replace the endpoint already is, and the two things an edit must never touch —
/// a configured timeout it did not ask about, and the enabled flag — survive it.
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class EditAutomation_Should_Constraint(AppHostFixture fixture)
{
    [Fact]
    public async Task EditingATrigger_Should_ReplaceItWithoutLosingTheAutomation()
    {
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Edit — the trigger changes");
        var id = await CreateAutomation(page, projectId, "ai:before", timeoutMinutes: null);

        await OpenAutomations(page, projectId);
        await Edit(page, "ai:before");

        var trigger = page.GetByLabel("Trigger label");
        await trigger.FillAsync("ai:after");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save changes" }).ClickAsync();

        // The identity survives — which is the whole point, since delete-and-recreate was the
        // workaround and every Run references the Automation it ran (BR-014).
        var stored = await Eventually(
            () => Automations(page, projectId),
            automations => automations.Single().TriggerLabel == "ai:after"
        );
        stored.Single().Id.ShouldBe(id);
        stored.Single().TriggerLabel.ShouldBe("ai:after");
    }

    [Fact]
    public async Task AnUnrelatedEdit_Should_LeaveAConfiguredTimeoutAlone()
    {
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Edit — the timeout survives");
        await CreateAutomation(page, projectId, "ai:timed", timeoutMinutes: 45);

        await OpenAutomations(page, projectId);
        await Edit(page, "ai:timed");

        // Only the label is touched. Before this change the form sent timeoutMinutes: null, and the
        // endpoint is a full replace — so this save would have quietly reset 45 to the default and
        // the row would have gone on showing a number.
        await page.GetByLabel("Trigger label").FillAsync("ai:timed-renamed");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save changes" }).ClickAsync();

        var stored = await Eventually(
            () => Automations(page, projectId),
            automations => automations.Single().TriggerLabel == "ai:timed-renamed"
        );
        stored.Single().TimeoutMinutes.ShouldBe(45);
    }

    [Fact]
    public async Task EditingADisabledAutomation_Should_LeaveItDisabled()
    {
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Edit — enabled is not ours");
        var id = await CreateAutomation(page, projectId, "ai:off", timeoutMinutes: null);

        (
            await page.APIRequest.PostAsync(
                $"{fixture.ServerBaseUrl}api/projects/{projectId}/automations/{id}/disable",
                new APIRequestContextOptions { DataObject = new { } }
            )
        ).Status.ShouldBe(200);

        await OpenAutomations(page, projectId);
        await Edit(page, "ai:off");
        await page.GetByLabel("Trigger label").FillAsync("ai:off-renamed");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save changes" }).ClickAsync();

        // Guaranteed by the update command's shape rather than by care — it has no Enabled member.
        // Asserted anyway, because that guarantee lasts exactly as long as the shape does.
        var stored = await Eventually(
            () => Automations(page, projectId),
            automations => automations.Single().TriggerLabel == "ai:off-renamed"
        );
        stored.Single().Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task TheCatalogue_Should_StillOfferEveryCapabilityAfterTheCanvasWentAway()
    {
        // #310 AC 11 / task 9.3, and ADR-0006's discipline: 522 lines of canvas were deleted, and
        // deleting a surface must not quietly take a capability with it. So each of UC-005's and
        // UC-006's five acts is driven here, on the tab that is left, in one pass — create, edit,
        // disable, re-enable, delete — and the canvas's absence is asserted rather than assumed.
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, $"Catalogue — {Guid.NewGuid():N}");

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=automations");

        // Create.
        await page.GetByRole(AriaRole.Button, new() { Name = "New Automation" })
            .ClickAsync(new() { Timeout = 30_000 });
        await page.Locator("#trigger-label").FillAsync("cat:one");
        await page.Locator("#prompt-path").FillAsync("one.md");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add Automation" }).ClickAsync();

        var created = await Eventually(
            () => Automations(page, projectId),
            automations => automations.Count == 1
        );
        created.Single().TriggerLabel.ShouldBe("cat:one");

        // Edit.
        await Edit(page, "cat:one");
        await page.GetByLabel("Trigger label").FillAsync("cat:renamed");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save changes" }).ClickAsync();
        await Eventually(
            () => Automations(page, projectId),
            automations => automations.Single().TriggerLabel == "cat:renamed"
        );

        // Disable, then re-enable — both from the panel's footer, where they live since the design
        // review moved them off the row. The panel deliberately stays open after either press, so it
        // is closed between them: the footer names the *other* action, and it learns which that is
        // when the panel reopens on freshly read data.
        await Edit(page, "cat:renamed");
        await page.GetByRole(AriaRole.Button, new() { Name = "Disable" }).ClickAsync();
        await Eventually(
            () => Automations(page, projectId),
            automations => !automations.Single().Enabled
        );
        await Close(page);

        await Edit(page, "cat:renamed");
        await page.GetByRole(AriaRole.Button, new() { Name = "Enable" }).ClickAsync();
        await Eventually(
            () => Automations(page, projectId),
            automations => automations.Single().Enabled
        );
        await Close(page);

        // Delete, which asks twice.
        await Edit(page, "cat:renamed");
        await page.GetByRole(AriaRole.Button, new() { Name = "Delete…" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Delete it permanently" })
            .ClickAsync();
        await Eventually(() => Automations(page, projectId), automations => automations.Count == 0);

        (await Automations(page, projectId)).ShouldBeEmpty();

        // And no canvas: the drag-to-chain payload's drop targets are gone with the surface, so
        // nothing on this tab is draggable any more.
        (await page.Locator("[draggable=true]").CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task AnEditThatCollides_Should_ShowTheApisOwnReason()
    {
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Edit — the refusal is the API's");
        await CreateAutomation(page, projectId, "ai:taken", timeoutMinutes: null);
        await CreateAutomation(page, projectId, "ai:free", timeoutMinutes: null);

        await OpenAutomations(page, projectId);
        await Edit(page, "ai:free");
        await page.GetByLabel("Trigger label").FillAsync("ai:taken");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save changes" }).ClickAsync();

        // The API's sentence, not a generic line: it names the trigger collided with and what to do
        // about it, which is the part a generic message throws away.
        var alert = page.GetByRole(AriaRole.Alert);
        await alert.First.WaitForAsync(new() { Timeout = 15_000 });
        var reason = await alert.First.TextContentAsync();
        reason.ShouldNotBeNull();
        reason.ShouldContain("would match the same Stories");

        // And nothing changed: a refused save is not a partial save.
        var stored = await Automations(page, projectId);
        stored.Count(automation => automation.TriggerLabel == "ai:taken").ShouldBe(1);
        stored.ShouldContain(automation => automation.TriggerLabel == "ai:free");
    }

    async Task OpenAutomations(IPage page, Guid projectId)
    {
        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=automations");
        await page.GetByRole(AriaRole.Button, new() { Name = "Edit" })
            .First.WaitForAsync(new() { Timeout = 30_000 });
    }

    /// <summary>Closes the edit panel, which enable and disable deliberately leave open.</summary>
    static Task Close(IPage page) =>
        page.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();

    /// <summary>Scoped to the row, because every row carries an Edit of its own.</summary>
    static Task Edit(IPage page, string triggerLabel) =>
        page.GetByRole(AriaRole.Listitem)
            .Filter(new() { HasText = triggerLabel })
            .GetByRole(AriaRole.Button, new() { Name = "Edit" })
            .First.ClickAsync();

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
        int? timeoutMinutes
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
                    timeoutMinutes,
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

    sealed record StoredAutomation(Guid Id, string TriggerLabel, int TimeoutMinutes, bool Enabled);
}
