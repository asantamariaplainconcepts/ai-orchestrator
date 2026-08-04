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

        // An unconfigured project lands on Settings with the form open (dashboard-tabs design
        // D3): the one day configuration IS the job. Seeing the Connector heading also proves
        // the route resolved and the bundle executed — a server-side fallback alone would have
        // rendered the shell without it.
        var heading = page.GetByRole(AriaRole.Heading, new() { Name = "Connector", Level = 2 });
        await heading.WaitForAsync(new() { Timeout = 30_000 });

        // Settings states the absence and what to do about it.
        var settingsEmptyState = page.GetByText("No backlog connected", new() { Exact = false });
        (await settingsEmptyState.IsVisibleAsync()).ShouldBeTrue();

        // Operate states its own absence, and points at the tab that fixes it — each tab owns
        // the message for what is missing from it.
        await page.GetByRole(AriaRole.Tab, new() { Name = "Operate" }).ClickAsync();

        var operateEmptyState = page.GetByText("Nothing to show yet", new() { Exact = false });
        await operateEmptyState.WaitForAsync(new() { Timeout = 15_000 });
        (await operateEmptyState.IsVisibleAsync()).ShouldBeTrue();

        // Refresh is meaningless without a Connector, and an enabled button that always fails is
        // worse than one that is honestly unavailable.
        var refresh = page.GetByRole(AriaRole.Button, new() { Name = "Refresh backlog" });
        (await refresh.IsDisabledAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task EveryImplementedRuntimeAndVendor_Should_BeSelectableFromTheForm()
    {
        // ADR-0006. Both of this project's seams have now shipped a second implementation that
        // was complete, tested, and impossible to choose — one behind a disabled control, one
        // behind a hardcoded constant. Asserting at the seam cannot catch that; only asserting
        // at the control a human actually uses can.
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Reachability");

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Connector", Level = 2 })
            .WaitForAsync(new() { Timeout = 30_000 });

        // Since dashboard-tabs these controls live on the Automations tab, and creation sits
        // behind an explicit button. Reachable by navigation is still reachable — which is the
        // distinction this test exists to police: relocated is fine, unreachable is not.
        await page.GetByRole(AriaRole.Tab, new() { Name = "Automations" }).ClickAsync();

        // The one-click defaults are gone WITH their capability (#162, DEC-062): the set was "one
        // of each action", and there is one action now. This test policed a button whose absence
        // would have been a regression; today its absence is the specification.
        await page.GetByRole(AriaRole.Button, new() { Name = "New Automation" }).ClickAsync();

        var runtime = page.Locator("#runtime");
        await runtime.WaitForAsync(new() { Timeout = 15_000 });
        (await runtime.IsDisabledAsync()).ShouldBeFalse();
        var runtimes = await runtime.Locator("option").AllInnerTextsAsync();
        runtimes.ShouldContain("ClaudeCodeHeadless");
        runtimes.ShouldContain("OpenCode");

        // One action, offered rather than hidden (ADR-0006 still applies to it), and the prompt
        // field beside it required — an Automation that names no prompt could never run.
        var action = page.Locator("#action");
        (await action.Locator("option").AllInnerTextsAsync()).ShouldHaveSingleItem();
        (await page.Locator("#prompt-path").GetAttributeAsync("required")).ShouldNotBeNull();

        // The inbox is a capability too (UC-026): its nav entry must exist and lead somewhere.
        var inboxNav = page.GetByRole(AriaRole.Link, new() { Name = "Inbox" });
        (await inboxNav.IsVisibleAsync()).ShouldBeTrue();

        await page.GetByRole(AriaRole.Tab, new() { Name = "Settings" }).ClickAsync();

        var vendor = page.Locator("#vendor");
        await vendor.WaitForAsync(new() { Timeout = 15_000 });
        (await vendor.IsDisabledAsync()).ShouldBeFalse();
        var vendors = await vendor.Locator("option").AllInnerTextsAsync();
        vendors.Count.ShouldBe(2);
        // Substring, not equality: the Azure DevOps option carries its unexercised warning.
        vendors.ShouldContain(text => text.Contains("Azure DevOps"));
    }

    [Fact]
    public async Task ProjectPage_Should_ReportTheFailure_WhenTheConnectorCannotBeConfigured()
    {
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Backlog page — unknown secret");

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Connector", Level = 2 })
            .WaitForAsync(new() { Timeout = 30_000 });

        // Exact: the shell's environment chip is labelled "This machine — owner · no sign-in",
        // which a substring "Owner" match also resolves to (design review 5a).
        await page.GetByLabel("Owner", new() { Exact = true }).FillAsync("acme");
        await page.GetByLabel("Repository").FillAsync("portal");
        await page.GetByRole(AriaRole.Button, new() { Name = "Name an existing secret instead" })
            .ClickAsync();
        await page.GetByLabel("Secret name").FillAsync("no-such-secret-in-this-environment");
        await page.GetByRole(AriaRole.Button, new() { Name = "Configure connector" }).ClickAsync();

        // The point of the assertion: a rejected configuration surfaces as an error the operator
        // can see, not as a silently unchanged form.
        var failure = page.GetByRole(AriaRole.Alert);
        await failure.WaitForAsync(new() { Timeout = 15_000 });

        var message = await failure.TextContentAsync();
        message.ShouldNotBeNull();
        // The API's own reason, not a generic line (#124): a refusal that names what is wrong
        // is the answer, and this one names the secret the environment does not have.
        message.ShouldContain("no-such-secret-in-this-environment");

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
        fixture.GitHub.Issues.Add(
            new StubIssue(41, "Ship the connector", "open", ["ai:implement"])
        );
        fixture.GitHub.Issues.Add(new StubIssue(42, "Wire the poller", "open", []));

        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Backlog page — mirrored");

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

    [Fact]
    public async Task StoryDetail_Should_RenderTheDescriptionAndNeutraliseHostileMarkdown()
    {
        // The body is untrusted input from whatever repository a project points at, so the
        // security claim is asserted against the rendered DOM in a real browser — the only
        // place "no script ran" is a fact rather than a hope (design D2, ADR-0004).
        fixture.GitHub.Repositories.Add("acme/portal");
        fixture.GitHub.Issues.Clear();
        fixture.GitHub.Issues.Add(
            new StubIssue(
                77,
                "Hostile story",
                "open",
                [],
                "## The requirement\n\nSomething **real** here.\n\n"
                    + "<script>window.__pwned = true;</script>\n\n"
                    + "[click me](javascript:window.__pwned=true)"
            )
        );

        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Story detail — hostile markdown");

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

        await page.GetByRole(AriaRole.Link, new() { Name = "Hostile story" })
            .ClickAsync(new() { Timeout = 20_000 });

        // The real content rendered as markdown…
        await Assertions
            .Expect(page.GetByRole(AriaRole.Heading, new() { Name = "The requirement" }))
            .ToBeVisibleAsync(new() { Timeout = 20_000 });

        // …the script never executed…
        (await page.EvaluateAsync<bool?>("() => window.__pwned ?? null")).ShouldBeNull();

        // …no script element survived sanitising…
        (await page.Locator(".prose script").CountAsync()).ShouldBe(0);

        // …and the javascript: href is gone rather than merely unclicked.
        var hostileHref = await page.Locator(".prose a").First.GetAttributeAsync("href");
        (hostileHref ?? string.Empty).ShouldNotStartWith("javascript:");
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
