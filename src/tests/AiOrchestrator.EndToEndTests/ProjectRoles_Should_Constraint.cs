using System.Text.Json;
using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// #13 / UC-002 — the roles surface, in a browser, on the Settings tab where it lives.
/// <para>
/// What this tier can prove and what it cannot, stated rather than blurred. The AppHost composes the
/// habitat with <b>one</b> caller — the machine's owner, no provider — so there is no second person
/// to grant anything to and no sign-in to perform. The grant itself, the roster afterwards, the
/// refusal for somebody who has never signed in and the last-administrator dead end are all covered
/// against the real API by <c>ProjectRoleAssignment_Should_Constraint</c>, which composes the habitat
/// that has roles at all.
/// </para>
/// <para>
/// What is left is exactly what only a browser can check, and it is not nothing: that the panel is on
/// the tab a human would look on, that it renders for somebody holding Admin, and that with nothing
/// granted it says so in words instead of showing an empty form. An honestly empty state is the state
/// every new project starts in.
/// </para>
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class ProjectRoles_Should_Constraint(AppHostFixture fixture)
{
    [Fact]
    public async Task TheSettingsTab_Should_CarryThePeoplePanelAndSayWhatIsTrue()
    {
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, $"Roles — {Guid.NewGuid():N}");

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=settings");

        // Present, and on Settings: configuring who may act is configuration, so it belongs beside
        // the Connector and the retirement panel rather than on its own screen.
        var heading = page.GetByRole(AriaRole.Heading, new() { Name = "People", Level = 2 });
        await heading.WaitForAsync(new() { Timeout = 30_000 });

        // It renders at all only because /api/me reports Admin on this project — the panel returns
        // null otherwise. So reaching this line is the role read working end to end.
        var explainer = page.GetByText("Admins configure this project", new() { Exact = false });
        (await explainer.IsVisibleAsync()).ShouldBeTrue();

        // Two truths about a project nobody has been granted anything on, in this habitat: no
        // holders, and nobody to offer — because a role belongs to a provider identity and this
        // deployment has no provider, so it has met nobody it could name (design D6).
        (
            await page.GetByText("Nobody has been given a role here yet").IsVisibleAsync()
        ).ShouldBeTrue();
        (
            await page.GetByText("already has a role here", new() { Exact = false })
                .IsVisibleAsync()
        ).ShouldBeTrue();

        // And no form offering a select with nothing in it, which is the shape this replaced.
        (await page.GetByLabel("Person").CountAsync()).ShouldBe(0);
    }

    async Task<string> CreateProject(IPage page, string name)
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
        return document.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("The created project has no id.");
    }
}
