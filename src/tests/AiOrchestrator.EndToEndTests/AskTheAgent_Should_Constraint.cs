using System.Text.Json;
using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// #166 — the conversation surface, in a browser.
/// <para>
/// <b>What this tier can prove, and what it deliberately does not.</b> Sending a message runs a real
/// agent pass, and this habitat composes the in-process runtime: it would clone a repository and
/// call a model, which CI has neither the credentials nor the minutes for. The message round trip —
/// one pass per message, usage recorded, an unmeasured pass reading unknown, a failure leaving the
/// conversation open — is covered against the real API in
/// <c>PortalConversation_Should_Constraint</c>, with the runtime stubbed at its seam.
/// </para>
/// <para>
/// What is left is what only a browser can check: that a Member can find this at all, that starting
/// a conversation about nothing is an ordinary path rather than a validation error, and that the
/// surface then asks for a message instead of for a subject.
/// </para>
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class AskTheAgent_Should_Constraint(AppHostFixture fixture)
{
    [Fact]
    public async Task AConversationAboutTheProject_Should_BeStartableWithNoSubject()
    {
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, $"Ask — {Guid.NewGuid():N}");

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=ask");

        // Reachable by navigation, which is the half of the capability a browser owns.
        await page.GetByRole(AriaRole.Heading, new() { Name = "Ask the agent", Level = 2 })
            .WaitForAsync(new() { Timeout = 30_000 });

        // The subject is optional and the label says so — an absent subject is the commonest
        // question ("what would you do here"), not a field somebody forgot.
        var subject = page.GetByLabel("Story (optional)");
        await subject.WaitForAsync(new() { Timeout = 15_000 });
        (await subject.InputValueAsync()).ShouldBeEmpty();

        await page.GetByRole(AriaRole.Button, new() { Name = "Start" }).ClickAsync();

        // Started: the surface now asks for a message, and says what it is about. Waited for, not
        // read once — starting is a mutation, and a single read races it (#107's lesson).
        await page.GetByLabel("Message").WaitForAsync(new() { Timeout = 15_000 });
        await page.GetByText("About this project", new() { Exact = false })
            .WaitForAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task TheAskTab_Should_NotBorrowTheRunVocabulary()
    {
        // A conversation occupies nothing and blocks nothing, so the surface must not look like a
        // Run's. This is the assertion that would catch somebody adding a state badge or a cancel
        // button here because the neighbouring tab has them.
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, $"Vocabulary — {Guid.NewGuid():N}");

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=ask");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Ask the agent", Level = 2 })
            .WaitForAsync(new() { Timeout = 30_000 });

        var panel = page.Locator("main");
        var text = await panel.TextContentAsync();
        text.ShouldNotBeNull();

        text.ShouldNotContain("Cancel run");
        text.ShouldNotContain("Queued");
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
