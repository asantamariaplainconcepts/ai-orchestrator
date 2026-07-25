using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// The smoke journey. It proves the composition end to end: the host boots against real
/// containers, serves the SPA same-origin, and the SPA reaches the API on a relative path.
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class SpaShell_Should_Constraint(AppHostFixture fixture)
{
    [Fact]
    public async Task Host_Should_ServeTheSpaShellSameOrigin()
    {
        var page = await fixture.Browser.NewPageAsync();

        await page.GotoAsync(fixture.ServerBaseUrl);

        // The heading comes from the i18n catalog, so seeing it proves the bundle executed —
        // not merely that some HTML was returned.
        var heading = page.GetByRole(AriaRole.Heading, new() { Name = "Projects" });
        await heading.WaitForAsync(new() { Timeout = 30_000 });

        (await heading.IsVisibleAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task Host_Should_ServeTheApiOnItsReservedPrefix()
    {
        var page = await fixture.Browser.NewPageAsync();

        var response = await page.APIRequest.GetAsync($"{fixture.ServerBaseUrl}api/projects");

        response.Status.ShouldBe(
            200,
            $"body: {await response.TextAsync()}\n\n{fixture.ServerLogTail()}"
        );
    }
}
