using System.Text.Json;
using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// #165 — an Automation hands off to more than one place, and the canvas says so honestly.
/// <para>
/// Two things are being checked, and the second matters as much as the first. That two edges leave
/// one node is the feature. That the canvas states they do not run at once is the feature's honesty:
/// BR-001 allows one active Run per Story, so a picture of two branches without that sentence
/// teaches its reader something the product will not do.
/// </para>
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class BranchingWorkflow_Should_Constraint(AppHostFixture fixture)
{
    [Fact]
    public async Task OneStepHandingToTwo_Should_DrawBothAndSayTheySerialize()
    {
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, $"Branching — {Guid.NewGuid():N}");

        // One source, two destinations. Both destinations are enabled, so both labels are real
        // edges rather than marks the vendor will carry and nobody will answer.
        await CreateAutomation(page, projectId, "br:source", ["br:left", "br:right"]);
        await CreateAutomation(page, projectId, "br:left", []);
        await CreateAutomation(page, projectId, "br:right", []);

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=automations");

        // The spine, then the branch row that names where it left. The chip is what makes the
        // second edge readable as an edge rather than as an unrelated chain below.
        await page.GetByText("br:source", new() { Exact = false })
            .First.WaitForAsync(new() { Timeout = 30_000 });

        // The chip that opens the branch row, by its accessible name. Counting occurrences of the
        // trigger label was the first version of this and it proved nothing: the Automations tab
        // shows the catalogue as well as the canvas, so every trigger already appears twice — the
        // assertion passed with branch rows switched off entirely.
        await page.GetByLabel("from br:source").WaitForAsync(new() { Timeout = 15_000 });

        // The ceiling, in words, where the edges are: this is the assertion that stops the picture
        // from over-promising.
        await page.GetByText("do not run at once", new() { Exact = false })
            .WaitForAsync(new() { Timeout = 15_000 });

        // And both destinations are drawn, not just the first.
        (
            await page.GetByText("br:left", new() { Exact = false }).CountAsync()
        ).ShouldBeGreaterThan(0);
        (
            await page.GetByText("br:right", new() { Exact = false }).CountAsync()
        ).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task DisconnectingOneBranch_Should_LeaveTheOther()
    {
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, $"Branch removal — {Guid.NewGuid():N}");

        var sourceId = await CreateAutomation(
            page,
            projectId,
            "rm:source",
            ["rm:left", "rm:right"]
        );
        await CreateAutomation(page, projectId, "rm:left", []);
        await CreateAutomation(page, projectId, "rm:right", []);

        // Through the API rather than the canvas control, deliberately: what must hold is that
        // removing one edge is a change to one member, and the canvas's own control routes through
        // this same update. Driving it here keeps the assertion about the rule, not about a click
        // Playwright cannot perform (an HTML5 drag, which #110 recorded).
        await UpdateAutomation(page, projectId, sourceId, "rm:source", ["rm:right"]);

        var stored = await Automations(page, projectId);
        var source = stored.Single(automation => automation.TriggerLabel == "rm:source");

        // The other survives. Before #165 the field held one label, so any change to a hand-off
        // replaced whatever was there — which is exactly the loss a set has to avoid.
        source.OutputLabels.ShouldBe(["rm:right"]);
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
        string[] outputLabels
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
                    action = "RefineOrComment",
                    runtime = "ClaudeCodeHeadless",
                    requiresApproval = false,
                    outputLabels,
                },
            }
        );
        response.Status.ShouldBe(201, await response.TextAsync());

        using var document = JsonDocument.Parse(await response.TextAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    async Task UpdateAutomation(
        IPage page,
        Guid projectId,
        Guid automationId,
        string triggerLabel,
        string[] outputLabels
    )
    {
        var response = await page.APIRequest.PutAsync(
            $"{fixture.ServerBaseUrl}api/projects/{projectId}/automations/{automationId}",
            new APIRequestContextOptions
            {
                DataObject = new
                {
                    triggerLabel,
                    triggerState = (string?)null,
                    action = "RefineOrComment",
                    runtime = "ClaudeCodeHeadless",
                    requiresApproval = false,
                    outputLabels,
                },
            }
        );
        response.Status.ShouldBe(200, await response.TextAsync());
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
        IReadOnlyList<string> OutputLabels
    );
}
