using System.Text.Json;
using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// The project-page journey: reach a Project, see that no backlog is connected, and get a real
/// failure back when the Connector cannot be configured.
/// <para>
/// Deliberately never needs a GitHub token. Configuration resolves the secret <b>before</b> it
/// calls the vendor, so an unknown secret name fails deterministically and offline — the same
/// path a real misconfiguration takes. A test that needed a live credential would be a test CI
/// could not run, and one whose red would mean "the token expired" as often as "the page broke".
/// </para>
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class ProjectPage_Should_Constraint(AppHostFixture fixture)
{
    [Fact]
    public async Task ProjectPage_Should_OfferConnectorConfiguration_WhenNoBacklogIsConnected()
    {
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Backlog page — unconfigured");

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}");

        // Seeing the Connector heading proves the route resolved and the bundle executed; a
        // server-side fallback alone would have rendered the shell without it.
        var heading = page.GetByRole(AriaRole.Heading, new() { Name = "Connector", Level = 2 });
        await heading.WaitForAsync(new() { Timeout = 30_000 });

        var emptyState = page.GetByText("No backlog connected", new() { Exact = false });
        (await emptyState.IsVisibleAsync()).ShouldBeTrue();

        // Refresh is meaningless without a Connector, and an enabled button that always fails is
        // worse than one that is honestly unavailable.
        var refresh = page.GetByRole(AriaRole.Button, new() { Name = "Refresh backlog" });
        (await refresh.IsDisabledAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task ProjectPage_Should_ReportTheFailure_WhenTheConnectorCannotBeConfigured()
    {
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Backlog page — unknown secret");

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Connector", Level = 2 })
            .WaitForAsync(new() { Timeout = 30_000 });

        await page.GetByLabel("Owner").FillAsync("acme");
        await page.GetByLabel("Repository").FillAsync("portal");
        await page.GetByLabel("Secret name").FillAsync("no-such-secret-in-this-environment");
        await page.GetByRole(AriaRole.Button, new() { Name = "Configure connector" }).ClickAsync();

        // The point of the assertion: a rejected configuration surfaces as an error the operator
        // can see, not as a silently unchanged form.
        var failure = page.GetByRole(AriaRole.Alert);
        await failure.WaitForAsync(new() { Timeout = 15_000 });

        var message = await failure.TextContentAsync();
        message.ShouldNotBeNull();
        message.ShouldContain("Could not save the Connector");

        // And nothing was stored: a Connector that exists is one that works (UC-004).
        var backlog = await page.APIRequest.GetAsync(
            $"{fixture.ServerBaseUrl}api/projects/{projectId}/backlog"
        );
        backlog.Status.ShouldBe(200, await backlog.TextAsync());

        using var document = JsonDocument.Parse(await backlog.TextAsync());
        document.RootElement.GetProperty("connector").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task ProjectPage_Should_ShowTheMirroredBacklog_AfterTheConnectorIsConfigured()
    {
        fixture.GitHub.Repositories.Add("acme/portal");
        fixture.GitHub.Issues.Clear();
        fixture.GitHub.Issues.Add((41, "Ship the connector", "open", ["ai:implement"]));
        fixture.GitHub.Issues.Add((42, "Wire the poller", "open", []));

        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Backlog page — mirrored");

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Connector", Level = 2 })
            .WaitForAsync(new() { Timeout = 30_000 });

        await page.GetByLabel("Owner").FillAsync("acme");
        await page.GetByLabel("Repository").FillAsync("portal");
        await page.GetByLabel("Secret name").FillAsync(AppHostFixture.SecretName);
        await page.GetByRole(AriaRole.Button, new() { Name = "Configure connector" }).ClickAsync();

        // Configuration only verifies access; the mirror fills on the first poll.
        var refresh = page.GetByRole(AriaRole.Button, new() { Name = "Refresh backlog" });
        await refresh.WaitForAsync(new() { Timeout = 15_000 });
        await Assertions.Expect(refresh).ToBeEnabledAsync(new() { Timeout = 15_000 });
        await refresh.ClickAsync();

        // Both Stories, with their vendor ids and labels — the whole path exercised: Octokit
        // against the stub, reconciliation into Postgres, the query, and the page.
        await Assertions
            .Expect(page.GetByText("Ship the connector"))
            .ToBeVisibleAsync(new() { Timeout = 20_000 });
        await Assertions.Expect(page.GetByText("Wire the poller")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("ai:implement")).ToBeVisibleAsync();

        // And the stub really was the far end, rather than the page rendering something cached.
        fixture.GitHub.Requests.ShouldContain("/api/v3/repos/acme/portal/issues");
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
