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

        // The tier that ships, saying what it needs before anybody takes it. #269 removed the
        // portable tier, so this asserts the labelling rather than a count: what the requirement
        // fixes is that a tier declares its assumptions, not how many tiers exist.
        await page.GetByRole(
                AriaRole.Heading,
                new() { Name = "The spec-first workflow", Level = 3 }
            )
            .WaitForAsync(new() { Timeout = 15_000 });

        var text = await page.Locator("main").TextContentAsync();
        text.ShouldNotBeNull();
        text.ShouldContain("Requires:");
        text.ShouldContain("OpenSpec");

        // Saved under its own name, which is what keeps a starter off a path a team's own
        // implement.md may already occupy.
        text.ShouldContain("aio-implement.md");
    }

    [Fact]
    public async Task TheSurface_Should_OfferOnlyTheBoundedInstall()
    {
        // #190's "no writes" was deliberately narrowed by #214: the offer still writes nothing,
        // and the one write that exists is Install — a draft PR a human merges. Asserted where it
        // could regress in either direction: no scaffolding ceremony returns, and Install is not
        // offered where it cannot act (this fresh project has no Connector, so presence is
        // unknown and the button must be absent).
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

        // No Connector → no target path → Install has nowhere to write, so it is not offered.
        (await page.GetByRole(AriaRole.Button, new() { Name = "Install" }).CountAsync()).ShouldBe(
            0
        );

        // And the copy says a human reviews and merges what an install opens.
        (await page.Locator("main").TextContentAsync())!.ShouldContain("draft pull request");
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
