using System.Text.Json;
using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// #190 — the starter set, in a browser.
/// <para>
/// What only this tier can check: that an Admin finds the set beside the field that names a prompt
/// file, that the tiers are told apart <i>on the screen</i> rather than only in a payload, and that
/// the surface offers no way to write any of it. The last one is the decision the change turned on,
/// and a control that appeared later would be how it was quietly reversed.
/// </para>
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class StarterPrompts_Should_Constraint(AppHostFixture fixture)
{
    [Fact]
    public async Task TheSet_Should_BeReachableWithItsTiersToldApart()
    {
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, $"Starters — {Guid.NewGuid():N}");

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=automations");

        await page.GetByRole(AriaRole.Heading, new() { Name = "Starter prompts", Level = 2 })
            .WaitForAsync(new() { Timeout = 30_000 });

        // Both tiers present, and the second one saying what it needs before anybody takes it.
        await page.GetByRole(AriaRole.Heading, new() { Name = "Starters", Level = 3 })
            .WaitForAsync(new() { Timeout = 15_000 });
        await page.GetByRole(
                AriaRole.Heading,
                new() { Name = "The spec-first workflow", Level = 3 }
            )
            .WaitForAsync(new() { Timeout = 15_000 });

        var text = await page.Locator("main").TextContentAsync();
        text.ShouldNotBeNull();
        text.ShouldContain("Requires:");
        text.ShouldContain("OpenSpec");

        // The two implement prompts are distinguishable, which is the collision this change found:
        // one file name for both would have made only one of them takeable.
        text.ShouldContain("implement.md");
        text.ShouldContain("aio-implement.md");
    }

    [Fact]
    public async Task TheSurface_Should_OfferNoWayToWriteAny()
    {
        // The decision, asserted where it could be reversed. Copy and show are the whole interaction;
        // anything that scaffolds, commits, or opens a pull request would be #162's ceremony coming
        // back for one feature.
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, $"No writes — {Guid.NewGuid():N}");

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=automations");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Starter prompts", Level = 2 })
            .WaitForAsync(new() { Timeout = 30_000 });

        foreach (var forbidden in new[] { "Scaffold", "Add to repository", "Create pull request" })
        {
            (
                await page.GetByRole(AriaRole.Button, new() { Name = forbidden }).CountAsync()
            ).ShouldBe(0, forbidden);
        }

        // And the copy says plainly that nothing is written.
        (await page.Locator("main").TextContentAsync())!.ShouldContain("Nothing here is written");
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
