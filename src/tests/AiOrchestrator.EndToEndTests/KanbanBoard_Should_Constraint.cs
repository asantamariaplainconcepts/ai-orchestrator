using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// #110's acceptance criteria 2 and 6, asserted where they are facts: a move made without
/// dragging reaches the vendor, and ordinary matching turns it into a Run. Nothing here knows
/// about dispatch — that is the whole claim.
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class KanbanBoard_Should_Constraint(AppHostFixture fixture)
{
    [Fact]
    public async Task MovingAStory_Should_LabelItAtTheVendorAndStartARun()
    {
        fixture.GitHub.Repositories.Add("acme/portal");
        fixture.GitHub.Issues.Clear();
        fixture.GitHub.Issues.Add(new StubIssue(51, "Board move", "open", []));

        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Board — move starts a Run");

        // An Automation, so the board has a column to move into and matching has something to
        // match. RefineOrComment executes without touching a workspace.
        var automation = await page.APIRequest.PostAsync(
            $"{fixture.ServerBaseUrl}api/projects/{projectId}/automations",
            new APIRequestContextOptions
            {
                DataObject = new
                {
                    triggerLabel = "ai:refine",
                    triggerState = (string?)null,
                    action = "RepositoryPrompt",
                    runtime = "ClaudeCodeHeadless",
                    promptPath = "story.md",
                    requiresApproval = false,
                },
            }
        );
        automation.Status.ShouldBe(201, await automation.TextAsync());

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Connector", Level = 2 })
            .WaitForAsync(new() { Timeout = 30_000 });

        await page.GetByLabel("Owner", new() { Exact = true }).FillAsync("acme");
        await page.GetByLabel("Repository").FillAsync("portal");
        // Pasting is the form's default (#124); this habitat has no writable store, so these
        // tests take the path that names a secret the environment already supplies.
        await page.GetByRole(AriaRole.Button, new() { Name = "Name an existing secret instead" })
            .ClickAsync();
        await page.GetByLabel("Secret name").FillAsync(AppHostFixture.SecretName);
        await page.GetByRole(AriaRole.Button, new() { Name = "Configure connector" }).ClickAsync();

        var refresh = page.GetByRole(AriaRole.Button, new() { Name = "Refresh backlog" });
        await refresh.WaitForAsync(new() { Timeout = 15_000 });
        await Assertions.Expect(refresh).ToBeEnabledAsync(new() { Timeout = 15_000 });
        await refresh.ClickAsync();

        await Assertions
            .Expect(page.GetByText("Board move"))
            .ToBeVisibleAsync(new() { Timeout = 20_000 });

        await page.GetByRole(AriaRole.Button, new() { Name = "Board view" }).ClickAsync();

        // Criterion 6: the move is made without dragging. Playwright cannot perform an HTML5
        // drag anyway, which is precisely why the menu is the semantics and the drag is sugar
        // (design D1) — the path a test can drive is the path a keyboard user has. Since the
        // design review that path is the card's ⋯ actions menu: revealed on hover and focus,
        // never out of the accessibility tree.
        var cardActions = page.GetByLabel("Card actions").First;
        await cardActions.WaitForAsync(new() { Timeout = 15_000 });
        await cardActions.ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new() { Name = "ai:refine" }).ClickAsync();

        // Criterion 2, at the vendor: the label really landed on the far end.
        await Expect(
            () =>
                fixture
                    .GitHub.Issues.Single(issue => issue.Number == 51)
                    .Labels.Contains("ai:refine"),
            "the label never reached the vendor stub"
        );

        // …and matching turned it into a Run, with no board-specific dispatch code involved.
        await Expect(
            async () =>
            {
                var response = await page.APIRequest.GetAsync(
                    $"{fixture.ServerBaseUrl}api/projects/{projectId}/runs"
                );
                if (response.Status != 200)
                {
                    return false;
                }

                using var document = JsonDocument.Parse(await response.TextAsync());
                return document.RootElement.GetArrayLength() > 0;
            },
            "moving the card produced no Run"
        );
    }

    /// <summary>Polls a synchronous condition to a deadline; xUnit has no built-in for this.</summary>
    static Task Expect(Func<bool> condition, string because) =>
        Expect(() => Task.FromResult(condition()), because);

    static async Task Expect(Func<Task<bool>> condition, string because)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(500);
        }

        throw new Exception(because);
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
