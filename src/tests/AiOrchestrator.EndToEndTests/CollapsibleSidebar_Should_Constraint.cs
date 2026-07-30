using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// #126 — the sidebar gives its width back and takes it again. What must hold: collapsed is a rail and
/// not a hidden panel (every destination one click away, the inbox count still visible), the choice
/// survives a reload, and below the medium breakpoint nothing changes.
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class CollapsibleSidebar_Should_Constraint(AppHostFixture fixture)
{
    [Fact]
    public async Task Collapsing_Should_GiveTheWidthToTheWorkAndKeepEveryDestination()
    {
        var page = await fixture.Browser.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 800);
        await page.GotoAsync($"{fixture.ServerBaseUrl}projects");

        var collapse = page.GetByRole(AriaRole.Button, new() { Name = "Collapse sidebar" });
        await collapse.WaitForAsync(new() { Timeout = 30_000 });

        var expanded = await SidebarWidth(page);
        await collapse.ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Expand sidebar" })
            .WaitForAsync(new() { Timeout = 10_000 });

        var collapsed = await SidebarWidth(page);

        // The canonical tokens, not a literal in the shell: 280 and 64.
        expanded.ShouldBe(280);
        collapsed.ShouldBe(64);

        // A rail, not a hidden panel — both destinations still there, one click each.
        var sidebar = page.Locator("aside");
        await sidebar
            .GetByRole(AriaRole.Link, new() { Name = "Projects" })
            .WaitForAsync(new() { Timeout = 5_000 });
        await sidebar
            .GetByRole(AriaRole.Link, new() { Name = "Inbox" })
            .WaitForAsync(new() { Timeout = 5_000 });
    }

    [Fact]
    public async Task ACollapsedSidebar_Should_StillShowWhatIsWaiting()
    {
        var page = await fixture.Browser.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 800);
        await page.GotoAsync($"{fixture.ServerBaseUrl}projects");

        var collapse = page.GetByRole(AriaRole.Button, new() { Name = "Collapse sidebar" });
        await collapse.WaitForAsync(new() { Timeout = 30_000 });
        await collapse.ClickAsync();

        // UC-026's ambient count is the reason this is a rail. Whether anything is actually waiting
        // depends on the environment, so the assertion is about the *entry*: it keeps its destination
        // and its name, which is what a count can be attached to.
        var inbox = page.Locator("aside").GetByRole(AriaRole.Link, new() { Name = "Inbox" });
        await inbox.WaitForAsync(new() { Timeout = 10_000 });
        (await inbox.GetAttributeAsync("title")).ShouldBe("Inbox");
    }

    [Fact]
    public async Task TheChoice_Should_SurviveAReload()
    {
        var page = await fixture.Browser.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 800);
        await page.GotoAsync($"{fixture.ServerBaseUrl}projects");

        var collapse = page.GetByRole(AriaRole.Button, new() { Name = "Collapse sidebar" });
        await collapse.WaitForAsync(new() { Timeout = 30_000 });
        await collapse.ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Expand sidebar" })
            .WaitForAsync(new() { Timeout = 10_000 });

        await page.ReloadAsync();

        // Still collapsed, and the control still offers the way back.
        await page.GetByRole(AriaRole.Button, new() { Name = "Expand sidebar" })
            .WaitForAsync(new() { Timeout = 15_000 });
        (await SidebarWidth(page)).ShouldBe(64);

        // And expanding is remembered too, not just collapsing.
        await page.GetByRole(AriaRole.Button, new() { Name = "Expand sidebar" }).ClickAsync();
        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Collapse sidebar" })
            .WaitForAsync(new() { Timeout = 15_000 });
        (await SidebarWidth(page)).ShouldBe(280);
    }

    [Fact]
    public async Task BelowTheBreakpoint_Should_OfferNoControlAtAll()
    {
        var page = await fixture.Browser.NewPageAsync();
        await page.SetViewportSizeAsync(375, 812);
        await page.GotoAsync($"{fixture.ServerBaseUrl}projects");

        // The folded sheet already is the collapsed state, so a second mechanism would be one idea
        // with two controls.
        await page.GetByRole(AriaRole.Button, new() { Name = "Open navigation" })
            .WaitForAsync(new() { Timeout = 30_000 });

        await Assertions
            .Expect(page.GetByRole(AriaRole.Button, new() { Name = "Collapse sidebar" }))
            .ToBeHiddenAsync();
        await Assertions
            .Expect(page.GetByRole(AriaRole.Button, new() { Name = "Expand sidebar" }))
            .ToBeHiddenAsync();

        // The sheet is the phone's sidebar, so it carries the identity block too (#178): a phone
        // user who cannot see who they are cannot end a session either — found by the owner on
        // the first mobile sign-in. This environment composes the LOCAL OWNER (observed, not
        // assumed: the first version of this assertion expected the stopgap's label and the
        // sheet said "Local owner"), so that is the name the block shows here.
        await page.GetByRole(AriaRole.Button, new() { Name = "Open navigation" }).ClickAsync();
        var sheet = page.GetByRole(AriaRole.Dialog);
        await sheet.WaitForAsync(new() { Timeout = 10_000 });
        var sheetText = await sheet.TextContentAsync();
        sheetText.ShouldNotBeNull();
        sheetText.ShouldContain("Local owner");
    }

    /// <summary>The rendered width, which is what the token is supposed to decide.</summary>
    static async Task<int> SidebarWidth(IPage page) =>
        await page.EvaluateAsync<int>(
            "() => Math.round(document.querySelector('aside').getBoundingClientRect().width)"
        );
}
