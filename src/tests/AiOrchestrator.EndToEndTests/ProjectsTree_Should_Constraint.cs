using System.Text.Json;
using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// UC-033 (#335) — the sidebar answers "what is every project doing" without a navigation per
/// project.
/// <para>
/// End-to-end because of one claim no cheaper test can make honestly: that the collapsed rail offers
/// the <b>same</b> children with the <b>same</b> destinations as the expanded tree (#126 design D2).
/// The components are shared so it ought to hold by construction, and this is what checks that the
/// construction is real — through a popover, at a width where nothing can be indented.
/// </para>
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class ProjectsTree_Should_Constraint(AppHostFixture fixture)
{
    [Fact]
    public async Task TheTree_Should_ShowAHeldStoryAndKeepItOnTheRail()
    {
        fixture.GitHub.Repositories.Add("acme/portal");
        fixture.GitHub.Issues.Clear();
        // Held at the vendor, which is where a hold lives (BR-007): the tree reads the mirror, so
        // seeding the label here is what makes the Story held everywhere.
        fixture.GitHub.Issues.Add(new StubIssue(77, "Tree — held story", "open", ["hitl"]));

        var page = await fixture.Browser.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 800);
        var projectName = "Tree — held work";
        var projectId = await CreateProject(page, projectName);
        await Configure(page, projectId);

        // The tree lives in the shell, so any page shows it. The projects list is the cheapest.
        await page.GotoAsync($"{fixture.ServerBaseUrl}projects");

        var sidebar = page.Locator("aside");
        var storyRow = sidebar.GetByRole(AriaRole.Link, new() { Name = "#77", Exact = false });

        // Expanded: the project row, the Story nested under it, and the hold said in words.
        await Assertions
            .Expect(sidebar.GetByRole(AriaRole.Link, new() { Name = projectName }))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Assertions.Expect(storyRow).ToBeVisibleAsync(new() { Timeout = 30_000 });

        var expandedHref = await storyRow.GetAttributeAsync("href");
        expandedHref.ShouldNotBeNull();
        expandedHref.ShouldContain($"/projects/{projectId}/stories/77");

        // The hold is a word, not a colour (design-contract): it survives greyscale because it is
        // written down.
        var sidebarText = await sidebar.TextContentAsync();
        sidebarText.ShouldNotBeNull();
        sidebarText.ShouldContain("Held");

        // Collapsed: the entry is still present, and opening it reveals the same destination. The
        // label is gone from the screen — the entry is not (#126 design D2).
        await page.GetByRole(AriaRole.Button, new() { Name = "Collapse sidebar" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Expand sidebar" })
            .WaitForAsync(new() { Timeout = 10_000 });

        var glyph = sidebar.GetByRole(AriaRole.Button, new() { Name = projectName });
        await glyph.WaitForAsync(new() { Timeout = 10_000 });
        await glyph.ClickAsync();

        var revealed = page.GetByRole(AriaRole.Link, new() { Name = "#77", Exact = false });
        await revealed.WaitForAsync(new() { Timeout = 10_000 });

        // The claim, asserted rather than assumed: same destination, from a 64px rail.
        (await revealed.GetAttributeAsync("href")).ShouldBe(expandedHref);
    }

    /// <summary>
    /// AC 2: a project with nothing in flight contributes its row and nothing else. Asserted as the
    /// absence of a group, because an empty container with a border is exactly the "no empty group,
    /// no placeholder" this forbids.
    /// </summary>
    [Fact]
    public async Task AQuietProject_Should_RenderAsItsRowAlone()
    {
        var page = await fixture.Browser.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 800);
        var projectName = "Tree — quiet project";
        await CreateProject(page, projectName);

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects");

        var sidebar = page.Locator("aside");
        var row = sidebar.GetByRole(AriaRole.Link, new() { Name = projectName });
        await row.WaitForAsync(new() { Timeout = 30_000 });

        // No connector, no stories, no Runs: the project is present and childless.
        await Assertions
            .Expect(sidebar.GetByRole(AriaRole.List, new() { Name = "Live work" }))
            .ToHaveCountAsync(0);
    }

    /// <summary>
    /// AC 6: the tree must not disturb the Inbox. Its entry and its destination are what the ambient
    /// count hangs on, so they are what this checks.
    /// </summary>
    [Fact]
    public async Task TheTree_Should_LeaveTheInboxEntryAlone()
    {
        var page = await fixture.Browser.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 800);
        await page.GotoAsync($"{fixture.ServerBaseUrl}projects");

        var inbox = page.Locator("aside").GetByRole(AriaRole.Link, new() { Name = "Inbox" });
        await inbox.WaitForAsync(new() { Timeout = 30_000 });

        (await inbox.GetAttributeAsync("href")).ShouldBe("/inbox");
    }

    async Task Configure(IPage page, string projectId)
    {
        var response = await page.APIRequest.PutAsync(
            $"{fixture.ServerBaseUrl}api/projects/{projectId}/connector",
            new APIRequestContextOptions
            {
                DataObject = new
                {
                    owner = "acme",
                    repository = "portal",
                    secretName = AppHostFixture.SecretName,
                },
            }
        );
        response.Ok.ShouldBeTrue(await response.TextAsync());

        // The mirror is what the tree reads, and a poll is what fills it.
        var refresh = await page.APIRequest.PostAsync(
            $"{fixture.ServerBaseUrl}api/projects/{projectId}/backlog/refresh",
            new APIRequestContextOptions { DataObject = new { } }
        );
        refresh.Ok.ShouldBeTrue(await refresh.TextAsync());
    }

    async Task<string> CreateProject(IPage page, string name)
    {
        var response = await page.APIRequest.PostAsync(
            $"{fixture.ServerBaseUrl}api/projects",
            new APIRequestContextOptions { DataObject = new { name } }
        );
        response.Status.ShouldBe(201, await response.TextAsync());

        using var document = JsonDocument.Parse(await response.TextAsync());
        return document.RootElement.GetProperty("id").GetString()!;
    }
}
