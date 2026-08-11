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
        // forgets. The hand-off is what the gesture is *about*; the model is what it must not touch.
        await CreateAutomation(
            page,
            projectId,
            "ai:grill",
            handsTo: "ready-for-proposal",
            model: "claude-sonnet-4-5"
        );
        await CreateAutomation(page, projectId, "ready-for-proposal");

        await Board(page, projectId);

        var control = page.GetByRole(
            AriaRole.Button,
            new() { Name = "Require a person after this step" }
        );
        await control.First.WaitForAsync(new() { Timeout = 30_000 });
        await control.First.ClickAsync();

        // Polled rather than read once: the click starts a mutation and returns, and a single read
        // races it — the flake #107 taught this repository to write out of its tests.
        var stored = await Eventually(
            () => Automations(page, projectId),
            automations =>
                automations
                    .Single(automation => automation.TriggerLabel == "ai:grill")
                    .OutputLabels.Count == 0
        );
        var grill = stored.Single(automation => automation.TriggerLabel == "ai:grill");

        // The gesture's own outcome, so a green model assertion cannot come from nothing happening.
        grill.OutputLabels.ShouldBeEmpty();

        // And the field the board cannot see is exactly as it was. This is the assertion that is red
        // before the fix.
        grill.Model.ShouldBe("claude-sonnet-4-5");
        grill.RequiresApproval.ShouldBeFalse();
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
        string? handsTo = null,
        string? model = null
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
                    outputLabels = handsTo is null ? Array.Empty<string>() : [handsTo],
                    model,
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
        string? Model,
        string? ToStage
    );
}
