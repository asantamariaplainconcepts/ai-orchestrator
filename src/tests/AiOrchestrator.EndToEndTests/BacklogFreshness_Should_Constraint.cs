using System.Text.Json;
using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// #340's acceptance criteria, asserted where they are facts: an open backlog catches up with the
/// Mirror on its own, focusing the tab is immediate, a hidden tab is idle, and none of it reaches
/// the vendor.
/// <para>
/// The harness disables the server's background poll (<c>Backlog__PollingEnabled = "false"</c>,
/// AppHostFixture), so these tests reconcile the Mirror out-of-band through
/// <c>page.APIRequest</c> — a request context separate from the browser, which never invalidates
/// the page's query cache. The page therefore has nothing to catch up *with* except the behaviour
/// under test, which makes the assertion sharper than a background poll would have been.
/// </para>
/// <para>
/// Visibility is driven by dispatching the real <c>visibilitychange</c> event TanStack's
/// focusManager listens to, rather than by <c>BringToFrontAsync</c>: the browser is headless, where
/// tab activation does not reliably move <c>document.visibilityState</c>.
/// </para>
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class BacklogFreshness_Should_Constraint(AppHostFixture fixture)
{
    /// <summary>The client interval (30s) plus slack for a round trip and a render.</summary>
    const int OneInterval = 40_000;

    /// <summary>AC 1 and AC 3 — one interval buys the list, the board and the detail together.</summary>
    [Fact]
    public async Task TheOpenSurfaces_Should_CatchUpWithoutAClick()
    {
        fixture.GitHub.Repositories.Add("acme/portal");
        fixture.GitHub.Issues.Clear();
        fixture.GitHub.Issues.Add(new StubIssue(61, "Before the vendor changed it", "open", []));

        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Freshness — catches up");
        await Automate(page, projectId, "ai:refine", "ai:refined");
        await Configure(page, projectId);
        await Reconcile(page, projectId);

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}");
        await Assertions
            .Expect(page.GetByText("Before the vendor changed it"))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });

        // The vendor moves underneath the open page: a new title *and* a trigger label, so one
        // interval settles AC 1 and AC 3 at once.
        fixture.GitHub.Issues.Clear();
        fixture.GitHub.Issues.Add(
            new StubIssue(61, "After the vendor changed it", "open", ["ai:refine"])
        );
        await Reconcile(page, projectId);

        // AC 1: the list catches up, with no click and no remount.
        await Assertions
            .Expect(page.GetByText("After the vendor changed it"))
            .ToBeVisibleAsync(new() { Timeout = OneInterval });

        // AC 3, the board: the card is now under the column the new label names.
        await page.GetByRole(AriaRole.Button, new() { Name = "Board view" }).ClickAsync();
        var column = page.GetByRole(AriaRole.Region, new() { Name = "ai:refine", Exact = true });
        await Assertions.Expect(column).ToBeVisibleAsync(new() { Timeout = OneInterval });
        await Assertions
            .Expect(column.GetByText("After the vendor changed it"))
            .ToBeVisibleAsync(new() { Timeout = OneInterval });

        // AC 3, the detail: the same label, on the Story's own screen.
        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}/stories/61");
        await Assertions
            .Expect(page.GetByText("ai:refine").First)
            .ToBeVisibleAsync(new() { Timeout = 30_000 });
    }

    /// <summary>AC 4 — the count of vendor calls is identical to the zero-tab case.</summary>
    [Fact]
    public async Task AnAutomaticReRead_Should_NotReachTheVendor()
    {
        fixture.GitHub.Repositories.Add("acme/portal");
        fixture.GitHub.Issues.Clear();
        fixture.GitHub.Issues.Add(new StubIssue(62, "Costs the vendor nothing", "open", []));

        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Freshness — no amplification");
        await Configure(page, projectId);
        await Reconcile(page, projectId);

        var reads = 0;
        page.Request += (_, request) =>
        {
            if (IsBacklogRead(request, projectId))
            {
                Interlocked.Increment(ref reads);
            }
        };

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}");
        await Assertions
            .Expect(page.GetByText("Costs the vendor nothing"))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });

        // Everything the vendor has been asked for up to this point. Nothing after this line is
        // allowed to add to it.
        var vendorCallsBefore = fixture.GitHub.Requests.Count;
        var readsBefore = Volatile.Read(ref reads);

        // The interval fires while the page sits open and untouched.
        await Expect(
            () => Volatile.Read(ref reads) > readsBefore,
            "the client never re-read the backlog on its own — the interval did not fire",
            OneInterval
        );

        // The re-read happened, and it was served from Postgres.
        fixture.GitHub.Requests.Count.ShouldBe(
            vendorCallsBefore,
            "an automatic re-read reached the vendor"
        );
    }

    /// <summary>AC 2 — focus re-reads even inside the app-wide 30s stale window.</summary>
    [Fact]
    public async Task FocusingTheTab_Should_ReReadInsideTheStaleWindow()
    {
        fixture.GitHub.Repositories.Add("acme/portal");
        fixture.GitHub.Issues.Clear();
        fixture.GitHub.Issues.Add(new StubIssue(63, "Immediate on return", "open", []));

        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Freshness — focus is immediate");
        await Configure(page, projectId);
        await Reconcile(page, projectId);

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}");
        await Assertions
            .Expect(page.GetByText("Immediate on return"))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });

        var reads = 0;
        page.Request += (_, request) =>
        {
            if (IsBacklogRead(request, projectId))
            {
                Interlocked.Increment(ref reads);
            }
        };

        fixture.GitHub.Issues.Clear();
        fixture.GitHub.Issues.Add(new StubIssue(63, "Changed while away", "open", []));
        await Reconcile(page, projectId);

        // Away and back well inside the 30s staleTime — a plain `refetchOnWindowFocus: true` would
        // be suppressed here, which is the whole reason the hook says "always".
        await Hide(page);
        await Show(page);

        await Assertions
            .Expect(page.GetByText("Changed while away"))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
        Volatile.Read(ref reads).ShouldBeGreaterThan(0, "focusing the tab issued no re-read");
    }

    /// <summary>AC 7 — a hidden tab issues nothing.</summary>
    [Fact]
    public async Task AHiddenTab_Should_IssueNoReRead()
    {
        fixture.GitHub.Repositories.Add("acme/portal");
        fixture.GitHub.Issues.Clear();
        fixture.GitHub.Issues.Add(new StubIssue(64, "Idle while hidden", "open", []));

        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Freshness — hidden is idle");
        await Configure(page, projectId);
        await Reconcile(page, projectId);

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}");
        await Assertions
            .Expect(page.GetByText("Idle while hidden"))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });

        await Hide(page);

        var reads = 0;
        page.Request += (_, request) =>
        {
            if (IsBacklogRead(request, projectId))
            {
                Interlocked.Increment(ref reads);
            }
        };

        // A full interval passes with the tab hidden.
        await Task.Delay(OneInterval);

        Volatile
            .Read(ref reads)
            .ShouldBe(0, "a hidden tab re-read the backlog — the interval is not gated on focus");
    }

    /// <summary>AC 5 — an automatic re-read degrades to stale, never to empty.</summary>
    [Fact]
    public async Task AFailedReconciliation_Should_LeaveTheMirroredStoriesReadable()
    {
        fixture.GitHub.Repositories.Add("acme/portal");
        fixture.GitHub.Issues.Clear();
        fixture.GitHub.Issues.Add(new StubIssue(65, "Stale beats empty", "open", []));

        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Freshness — stale not empty");
        await Configure(page, projectId);
        await Reconcile(page, projectId);

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}");
        await Assertions
            .Expect(page.GetByText("Stale beats empty"))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });

        // The vendor goes away: an unknown repository is a 404, which is how this stub fails.
        fixture.GitHub.Repositories.Remove("acme/portal");
        var refresh = await page.APIRequest.PostAsync(
            $"{fixture.ServerBaseUrl}api/projects/{projectId}/backlog/refresh",
            new APIRequestContextOptions { DataObject = new { } }
        );
        refresh.Ok.ShouldBeFalse("the stub answered a repository it should no longer know");

        // An automatic re-read now happens against a Connector carrying a failure.
        await Hide(page);
        await Show(page);

        // Still there. The failure did not empty the screen.
        await Assertions
            .Expect(page.GetByText("Stale beats empty"))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });

        fixture.GitHub.Repositories.Add("acme/portal");
    }

    /// <summary>
    /// Task 2.1's hazard, through the path the repository treats as the semantics: an automatic
    /// re-read must not overwrite an optimistic move with pre-move truth. The guard predates this
    /// change (<c>cancelQueries</c> in useMoveStory.onMutate) — this pins it against the interval.
    /// </summary>
    [Fact]
    public async Task AnAutomaticReRead_Should_NotClobberAnOptimisticMove()
    {
        fixture.GitHub.Repositories.Add("acme/portal");
        fixture.GitHub.Issues.Clear();
        fixture.GitHub.Issues.Add(new StubIssue(66, "Moved by the menu", "open", []));

        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, "Freshness — move survives");
        await Automate(page, projectId, "ai:refine", "ai:refined");
        await Configure(page, projectId);
        await Reconcile(page, projectId);

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}");
        await Assertions
            .Expect(page.GetByText("Moved by the menu"))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });
        await page.GetByRole(AriaRole.Button, new() { Name = "Board view" }).ClickAsync();

        var cardActions = page.GetByLabel("Card actions").First;
        await cardActions.WaitForAsync(new() { Timeout = 15_000 });
        await cardActions.ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new() { Name = "ai:refine", Exact = true })
            .ClickAsync();

        // Provoke a re-read immediately, while the move is settling.
        await Show(page);

        // The label really landed, and the card is under the column that names it — the re-read
        // did not put it back where it started.
        await Expect(
            () =>
                fixture
                    .GitHub.Issues.Single(issue => issue.Number == 66)
                    .Labels.Contains("ai:refine"),
            "the label never reached the vendor stub"
        );

        var column = page.GetByRole(AriaRole.Region, new() { Name = "ai:refine", Exact = true });
        await Assertions
            .Expect(column.GetByText("Moved by the menu"))
            .ToBeVisibleAsync(new() { Timeout = OneInterval });
    }

    static bool IsBacklogRead(IRequest request, string projectId) =>
        request.Method == "GET"
        && request.Url.Contains($"/api/projects/{projectId}/backlog", StringComparison.Ordinal)
        && !request.Url.Contains("/stories/", StringComparison.Ordinal);

    /// <summary>
    /// Drives the one signal TanStack's focusManager reads — it resolves focus as
    /// <c>document.visibilityState !== "hidden"</c> and listens for <c>visibilitychange</c>.
    /// </summary>
    static Task Hide(IPage page) => SetVisibility(page, "hidden");

    static Task Show(IPage page) => SetVisibility(page, "visible");

    static Task SetVisibility(IPage page, string state) =>
        page.EvaluateAsync(
            @"(state) => {
                Object.defineProperty(document, 'visibilityState', {
                    value: state,
                    configurable: true,
                });
                Object.defineProperty(document, 'hidden', {
                    value: state === 'hidden',
                    configurable: true,
                });
                document.dispatchEvent(new Event('visibilitychange', { bubbles: true }));
            }",
            state
        );

    /// <summary>Polls a condition to a deadline; xUnit has no built-in for this.</summary>
    static Task Expect(Func<bool> condition, string because, int timeoutMs = 30_000)
    {
        return Poll();

        async Task Poll()
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);

            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                {
                    return;
                }

                await Task.Delay(500);
            }

            throw new Exception(because);
        }
    }

    async Task Automate(IPage page, string projectId, string triggerLabel, string toStage)
    {
        var response = await page.APIRequest.PostAsync(
            $"{fixture.ServerBaseUrl}api/projects/{projectId}/automations",
            new APIRequestContextOptions
            {
                DataObject = new
                {
                    triggerLabel,
                    triggerState = (string?)null,
                    action = "RepositoryPrompt",
                    runtime = "ClaudeCodeHeadless",
                    promptPath = "story.md",
                    toStage,
                },
            }
        );
        response.Status.ShouldBe(201, await response.TextAsync());
    }

    async Task Configure(IPage page, string projectId)
    {
        var response = await page.APIRequest.PutAsync(
            $"{fixture.ServerBaseUrl}api/projects/{projectId}/connector",
            new APIRequestContextOptions
            {
                DataObject = new
                {
                    owner = "acme",
                    repository = "portal",
                    secretName = AppHostFixture.SecretName,
                },
            }
        );
        response.Ok.ShouldBeTrue(await response.TextAsync());
    }

    /// <summary>
    /// Reconciles the Mirror the way the server's own poll would, but out-of-band: this goes
    /// through the test's request context, so the browser's query cache is never invalidated.
    /// </summary>
    async Task Reconcile(IPage page, string projectId)
    {
        var response = await page.APIRequest.PostAsync(
            $"{fixture.ServerBaseUrl}api/projects/{projectId}/backlog/refresh",
            new APIRequestContextOptions { DataObject = new { } }
        );
        response.Ok.ShouldBeTrue(await response.TextAsync());
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
