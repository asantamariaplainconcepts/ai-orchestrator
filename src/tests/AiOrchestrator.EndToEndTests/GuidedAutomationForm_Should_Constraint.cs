using System.Text.Json;
using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// #231 — the New Automation form asks three questions instead of presenting eight peer fields.
/// <para>
/// What only a browser can check: that the grouping is actually visible, that the approval control
/// states its consequence where a reader will meet it, and that answering "stop" removes the
/// next-stage control rather than merely ignoring it. The request-shape guarantee is asserted in the
/// functional suite, where the payload can be read.
/// </para>
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class GuidedAutomationForm_Should_Constraint(AppHostFixture fixture)
{
    async Task<IPage> OpenForm(string name)
    {
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, name);

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=automations");
        await page.GetByRole(AriaRole.Button, new() { Name = "New Automation" })
            .ClickAsync(new() { Timeout = 30_000 });

        return page;
    }

    /// <summary>
    /// The form lives in a dialog since design review 6b, and a dialog is portalled to the end of the
    /// document — outside <c>main</c>. Assertions about the form's own text scope here rather than to
    /// the page, so they cannot pass on copy that happens to appear elsewhere on the tab.
    /// </summary>
    static ILocator Form(IPage page) => page.GetByRole(AriaRole.Dialog);

    [Fact]
    public async Task TheForm_Should_AskThreeQuestionsInExecutionOrder()
    {
        var page = await OpenForm($"Guided — {Guid.NewGuid():N}");

        // The order is the Automation's own: matching reads the trigger, the executor reads the
        // prompt, HandOn applies the labels. A reader filling it top to bottom has walked a Run.
        var headings = await page.GetByRole(AriaRole.Heading, new() { Level = 3 })
            .AllInnerTextsAsync();
        var questions = headings.Where(heading => heading.Contains('?')).ToList();

        questions.Count.ShouldBe(3);
        questions[0].ShouldContain("When does it fire");
        questions[1].ShouldContain("What does it do");
        questions[2].ShouldContain("What happens after");
    }

    [Fact]
    public async Task TheForm_Should_OfferNoApprovalControl()
    {
        // It used to be a switch that explained itself: the Agent plans, stops and waits. There is
        // nothing left to explain (#321, DEC-067) — a step that stops for a person marks the hold,
        // which is an ordinary mark in the field the form already has. Asserted in a browser
        // because "the control is gone" is exactly the kind of claim a unit test cannot make.
        var page = await OpenForm($"No approval — {Guid.NewGuid():N}");

        await Form(page).WaitForAsync(new() { Timeout = 15_000 });

        (await page.Locator("#requires-approval").CountAsync()).ShouldBe(0);

        var text = await Form(page).TextContentAsync();
        text.ShouldNotBeNull();
        text.ShouldNotContain("Nothing executes until someone approves");
    }

    [Fact]
    public async Task EndingTheChain_Should_BeAnAnswerRatherThanAnEmptyControl()
    {
        var page = await OpenForm($"After — {Guid.NewGuid():N}");

        var stop = page.Locator("#after-stop");
        await stop.WaitForAsync(new() { Timeout = 15_000 });

        // Stopping is the default and it is *chosen*: the next-stage control is absent, so there is no
        // empty field to mistake for "I have not got there yet".
        //
        // The field this watches is `#to-stage` since #310, not `#output-label`. The two swapped
        // meanings: the claimed transition is what "hand on or stop" answers, and the marks beside it
        // are offered whichever answer it holds — a mark is applied by an Automation that stops just
        // as readily as by one that hands on, so hiding that field would have made the radio decide
        // something it does not decide.
        (await stop.GetAttributeAsync("data-state")).ShouldBe("checked");
        (await page.Locator("#to-stage").CountAsync()).ShouldBe(0);
        (await page.Locator("#output-label").CountAsync()).ShouldBe(1);

        await page.Locator("#after-hand-on").ClickAsync();

        await page.Locator("#to-stage").WaitForAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task TheSentence_Should_RestateTheFormAndNameWhatIsMissing()
    {
        // Not a second validation channel (design D2): an incomplete form yields an incomplete
        // sentence naming the gap, and raises nothing.
        var page = await OpenForm($"Sentence — {Guid.NewGuid():N}");

        var form = Form(page);
        (await form.TextContentAsync())!.ShouldContain("name a trigger label");

        await page.Locator("#trigger-label").FillAsync("ai:review");

        // The trigger is now stated, and only the still-missing prompt is flagged.
        var afterTyping = await form.TextContentAsync();
        afterTyping.ShouldNotBeNull();
        afterTyping.ShouldContain("ai:review");
        afterTyping.ShouldNotContain("name a trigger label");
        afterTyping.ShouldContain("name a prompt file");
    }

    [Fact]
    public async Task Stopping_Should_StoreTheAbsentClaimItHasAlwaysMeant()
    {
        // The criterion regrouping must not break: "stop" is not a new concept downstream, it is a
        // claim on no transition — which is what the empty label set used to mean before #310 gave the
        // hand-off a field of its own. Asserted on what the API stored, not on what the form showed —
        // the frontend has no test runner, so the artifact is the only honest witness.
        var page = await fixture.Browser.NewPageAsync();
        var projectId = await CreateProject(page, $"Payload — {Guid.NewGuid():N}");

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=automations");
        await page.GetByRole(AriaRole.Button, new() { Name = "New Automation" })
            .ClickAsync(new() { Timeout = 30_000 });

        await page.Locator("#trigger-label").FillAsync("ai:review");
        await page.Locator("#prompt-path").FillAsync("review.md");

        // Name a next stage, THEN choose to stop. This is the only path where the two behaviours
        // differ: with nothing named, "stop" and "hand on" both store no claim, so a test that skipped
        // this would pass against the code this change replaced — the false green #189's retro is
        // about, and the mutation check caught it here.
        await page.Locator("#after-hand-on").ClickAsync();
        await page.Locator("#to-stage").FillAsync("ai:merge");
        await page.Locator("#after-stop").ClickAsync();
        (await page.Locator("#to-stage").CountAsync()).ShouldBe(0);

        // The Automation form's own submit. Named rather than located inside the `<form>`: since
        // design review 6b the button sits in the panel's footer and reaches the form by its id, so
        // that it stays put while the body scrolls. "Add Automation" is still the unambiguous name —
        // "Add" alone is also the output-label button.
        await page.GetByRole(AriaRole.Button, new() { Name = "Add Automation" }).ClickAsync();

        // The form closes on success, which is the signal the mutation landed. Waited for rather
        // than slept through — reading the API before the POST settles is a race that passes
        // locally and fails in CI.
        await page.GetByRole(AriaRole.Button, new() { Name = "New Automation" })
            .WaitForAsync(new() { Timeout = 15_000 });

        var listed = await page.APIRequest.GetAsync(
            $"{fixture.ServerBaseUrl}api/projects/{projectId}/automations"
        );
        listed.Status.ShouldBe(200, await listed.TextAsync());

        using var document = JsonDocument.Parse(await listed.TextAsync());
        var automation = document.RootElement.EnumerateArray().Single();

        automation.GetProperty("triggerLabel").GetString().ShouldBe("ai:review");
        automation.GetProperty("promptPath").GetString().ShouldBe("review.md");
        // Absent despite a stage having been named: the radio is the later, more explicit answer, and
        // honouring a value the Admin then said not to use would obey the field over the person.
        automation.GetProperty("toStage").ValueKind.ShouldBe(JsonValueKind.Null);
        // And no mark was invented out of the stage that was typed and withdrawn: the two fields are
        // separate things now, so one being declined must not spill into the other.
        automation.GetProperty("outputLabels").GetArrayLength().ShouldBe(0);
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
