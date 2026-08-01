using System.Text.Json;
using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// #189 — the scratchpad, in a browser.
/// <para>
/// <b>What this tier can prove, and what it deliberately does not.</b> Running an attempt runs a real
/// agent pass, and this habitat composes the in-process runtime: it would clone a repository and
/// call a model, which CI has neither the credentials nor the minutes for — the same limit
/// <c>AskTheAgent_Should_Constraint</c> states for the conversation it is built on. The pass itself,
/// the fresh-conversation-per-attempt rule and the fidelity of the Story framing are covered against
/// the real API in <c>PromptScratchpad_Should_Constraint</c> in the Runs functional suite.
/// </para>
/// <para>
/// What is left is what only a browser owns: that an Admin can find this beside the field that names
/// a prompt file, and that the two sentences carrying the things nothing else can carry — the text
/// is not saved, and a trial is not quite a Run — are actually on the screen.
/// </para>
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class PromptScratchpad_Should_Constraint(AppHostFixture fixture)
{
    [Fact]
    public async Task TheScratchpad_Should_SitBesideThePromptFileField()
    {
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, $"Scratchpad — {Guid.NewGuid():N}");

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=automations");

        await page.GetByRole(AriaRole.Heading, new() { Name = "Try a prompt", Level = 2 })
            .WaitForAsync(new() { Timeout = 30_000 });

        // Multi-line, because a prompt is a document rather than a chat line — and a single-line
        // field would make pasting one a worse experience than the file it replaces.
        var prompt = page.Locator("#scratchpad-prompt");
        await prompt.WaitForAsync(new() { Timeout = 15_000 });
        (await prompt.EvaluateAsync<string>("element => element.tagName")).ShouldBe("TEXTAREA");

        await prompt.FillAsync("Estimate this story in points.");
        (
            await page.GetByRole(AriaRole.Button, new() { Name = "Run once" }).IsDisabledAsync()
        ).ShouldBeFalse();
    }

    [Fact]
    public async Task TheSurface_Should_SayTheTextIsNotSavedAndWhereItBelongs()
    {
        // The one thing this surface must never let somebody assume. A scratchpad that looked like a
        // place a prompt lives would undo #150 and #162's premise by suggestion alone.
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, $"Not saved — {Guid.NewGuid():N}");

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=automations");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Try a prompt", Level = 2 })
            .WaitForAsync(new() { Timeout = 30_000 });

        var text = await page.Locator("main").TextContentAsync();
        text.ShouldNotBeNull();

        text.ShouldContain("not saved");
        text.ShouldContain("prompts directory");

        // And what a trial does not reproduce (design D4), stated on the screen rather than left to
        // be inferred from a Run that behaved differently.
        text.ShouldContain("requires approval");
        text.ShouldContain("timeout");
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
