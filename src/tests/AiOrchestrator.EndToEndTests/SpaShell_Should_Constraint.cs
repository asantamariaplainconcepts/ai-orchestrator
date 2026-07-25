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

        if (response.Status != 200)
        {
            // The health verdict at failure time distinguishes "database unusable" (the DB
            // check fails) from "endpoint bug" (health green while the endpoint 500s). The
            // delay lets the resource log stream drain — the request-time exception lags the
            // failure by a couple of seconds and an eager read misses it.
            var health = await page.APIRequest.GetAsync($"{fixture.ServerBaseUrl}api/health");
            await Task.Delay(TimeSpan.FromSeconds(4));
            response.Status.ShouldBe(
                200,
                $"body: {await response.TextAsync()}\n"
                    + $"health: {health.Status} {await health.TextAsync()}\n\n"
                    + fixture.ServerLogTail(lines: 100)
            );
        }
    }
}
