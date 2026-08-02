using System.Text.Json;
using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// #232 — the workflow reads top-down at every width, with one layout and one interaction.
/// <para>
/// The canvas used to be two products in one file: below <c>xl</c> it stacked and the drag was
/// hidden entirely, so a phone could not reorder a pipeline at all; at <c>xl</c> it flipped
/// horizontal and scrolled sideways. These cases assert at the width where the capability was
/// missing, because a desktop-only assertion is exactly what let it stay missing.
/// </para>
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class VerticalWorkflowCanvas_Should_Constraint(AppHostFixture fixture)
{
    const int PhoneWidth = 375;

    [Fact]
    public async Task ThePipeline_Should_BeReorderableOnAPhone()
    {
        // The capability that did not exist below xl. Asserted as "the control is reachable and
        // visible at 375px" rather than by performing a drag — Playwright cannot do an HTML5 drag
        // (#110, #137), which is why the explicit control carries the semantics here too.
        var page = await Canvas(PhoneWidth, "Vertical — phone");

        var block = page.Locator("[draggable=true]").First;
        await block.WaitForAsync(new() { Timeout = 15_000 });

        (await block.IsVisibleAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task TheChain_Should_ReadTopDownWithoutScrollingSideways()
    {
        var page = await Canvas(PhoneWidth, "Vertical — direction");

        // One layout: the chain is a column, and the page does not scroll horizontally. The old
        // xl fork made both of these false at desktop and the first true only by hiding the drag.
        var direction = await page.EvaluateAsync<string>(
            @"() => {
                const chain = [...document.querySelectorAll('div')].find(
                  (node) => typeof node.className === 'string' && node.className.includes('max-w-[520px]'));
                return chain ? getComputedStyle(chain).flexDirection : 'not-found';
            }"
        );
        direction.ShouldBe("column");

        // Scoped to the canvas, not the document. The page *does* overflow at this width — the
        // project tab strip is 528px wide and predates this change (filed separately). Asserting
        // on the document would have made this test fail for somebody else's defect, and pass only
        // once that was fixed, which is a test about the wrong thing.
        var canvasOverflows = await page.EvaluateAsync<bool>(
            @"() => {
                const chain = [...document.querySelectorAll('div')].find(
                  (node) => typeof node.className === 'string' && node.className.includes('max-w-[520px]'));
                if (!chain) return true;
                return chain.scrollWidth > chain.clientWidth + 1;
            }"
        );
        canvasOverflows.ShouldBeFalse();
    }

    [Fact]
    public async Task AnOpenGap_Should_OfferNoSelectUntilSomebodyIsConnecting()
    {
        // A select at every gap is a control offered to somebody who is not connecting anything.
        // It stays reachable, which is what ADR-0006 asks — one click, not zero.
        var page = await Canvas(1280, "Vertical — gap");

        var handsTo = page.GetByRole(AriaRole.Button, new() { Name = "Hands work to" });
        await handsTo.First.WaitForAsync(new() { Timeout = 15_000 });

        (await page.Locator("select[aria-label='Hands work to…']").CountAsync()).ShouldBe(0);

        await handsTo.First.ClickAsync();

        await page.Locator("select[aria-label='Hands work to…']")
            .First.WaitForAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AGatedStep_Should_WearTheBoardsOwnChip()
    {
        // A *chain*, with the first step gated. A lone Automation is deliberately not drawn — the
        // canvas says "No Automation hands work to another yet" — so seeding one and looking for a
        // node asserts a scenario this surface does not render. The first draft did exactly that
        // and spent thirty seconds waiting for something that was never going to appear.
        var page = await fixture.Browser.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        var projectId = await CreateProject(page, $"Vertical — gate {Guid.NewGuid():N}");
        await CreateAutomation(
            page,
            projectId,
            "ai:grill",
            outputLabel: "ready-for-proposal",
            requiresApproval: true
        );
        await CreateAutomation(page, projectId, "ready-for-proposal");

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=automations");
        await page.GetByText("ai:grill").First.WaitForAsync(new() { Timeout = 30_000 });

        // The same chip the board's column header uses, not a second one that looks like it — if
        // the wording changes in one place and not the other, the two surfaces stop agreeing about
        // what "a person approves here" is called.
        //
        // Located by the chip's own tooltip rather than by its text: "Approval" appears in several
        // places on this tab, and matching the first one in DOM order waits on whichever element
        // that happens to be. The tooltip is the surface-specific one the chip takes as a prop,
        // because the board's "dropping here…" sentence means nothing on a canvas.
        var chip = page.Locator("[title='A person approves the plan']");
        await chip.First.WaitForAsync(new() { Timeout = 30_000 });

        (await chip.First.TextContentAsync()).ShouldBe("Approval");
    }

    async Task<IPage> Canvas(int width, string name)
    {
        var page = await fixture.Browser.NewPageAsync();
        await page.SetViewportSizeAsync(width, 900);

        var projectId = await CreateProject(page, $"{name} {Guid.NewGuid():N}");
        await CreateAutomation(page, projectId, "ai:grill", outputLabel: "ready-for-proposal");
        await CreateAutomation(page, projectId, "ready-for-proposal");

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=automations");

        // Wait for a node, not for the heading. The "Workflow" heading renders before the
        // automations have loaded, so asserting straight after it races the fetch — the same
        // single-read-versus-mutation trap #107 recorded, and it made this suite flake between
        // runs before it was fixed.
        await page.GetByText("ai:grill").First.WaitForAsync(new() { Timeout = 30_000 });

        return page;
    }

    async Task CreateAutomation(
        IPage page,
        Guid projectId,
        string triggerLabel,
        string? outputLabel = null,
        bool requiresApproval = false
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
                    requiresApproval,
                    outputLabels = outputLabel is null ? Array.Empty<string>() : [outputLabel],
                },
            }
        );
        response.Status.ShouldBe(201, await response.TextAsync());
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
}
