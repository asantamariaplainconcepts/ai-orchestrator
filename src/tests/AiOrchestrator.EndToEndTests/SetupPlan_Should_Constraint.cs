using System.Text.Json;
using Microsoft.Playwright;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// #233 — the setup card says what it will create before the button, not after.
/// <para>
/// What only a browser can settle: that the plan is on screen <b>before</b> anything is pressed, and
/// that the install-missing checkbox is gone — it was standing in for a preview, and once the rows
/// say which steps install a starter it has nothing left to communicate.
/// </para>
/// <para>
/// The plan's *content* — which step wires which file, which one waits for a person — is asserted in
/// <c>PipelineAdoption_Should_Constraint</c>, against the API that computes it.
/// </para>
/// <para>
/// <b>What is deliberately not asserted here.</b> The plan rows and the draft-pull-request sentence
/// only render once discovery has succeeded, which needs a Connector serving directory listings —
/// and this tier's GitHub stub answers issues only. Reaching that state would mean extending the
/// stub, which is its own change. So the E2E covers what it can reach honestly: the checkbox is
/// gone. The rest lives in the functional suite, where the listing can be arranged.
/// </para>
/// <para>
/// <b>#262 inherits that boundary.</b> Making each plan row selectable adds a per-row checkbox, a
/// broken-hand-off marker and a confirm that goes unavailable on an empty selection — all of it
/// behind the same unreachable state, so none of it is asserted here. A test written against a
/// state this tier cannot enter would pass by never running its assertions, which is worse than the
/// gap it pretends to close. The selection's behaviour is pinned in
/// <c>PipelineAdoption_Should_Constraint</c> against the API that carries it.
/// </para>
/// <para>
/// <b>#269 inherits it too, with one exception worth naming.</b> The consent switch is the first part
/// of this card that renders <i>without</i> a successful discovery — it is deliberately outside the
/// plan list, because an empty repository has no rows and that is the case a consent exists for. In
/// principle it is reachable here. It is still not asserted, because the tier's stub project has no
/// Connector, so the card stops at its own "no Connector yet" answer before the tiers are read. The
/// consent's behaviour was verified against the running SPA in mock mode instead — off by default,
/// the plan growing when it is turned on, and the report separating written documents from ones left
/// alone — and its contract is pinned in <c>PipelineAdoption_Should_Constraint</c>. Extending the stub
/// to serve directory listings remains the change that would close all of this at once.
/// </para>
/// </summary>
[Collection(AppHostCollection.Name)]
[Trait("Category", "E2E")]
public class SetupPlan_Should_Constraint(AppHostFixture fixture)
{
    [Fact]
    public async Task TheCard_Should_OfferNoCheckboxStandingInForAPreview()
    {
        var page = await fixture.Browser.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        var projectId = await CreateProject(page, $"Setup plan — {Guid.NewGuid():N}");

        await page.GotoAsync($"{fixture.ServerBaseUrl}projects/{projectId}?tab=automations");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Set up the whole workflow" })
            .WaitForAsync(new() { Timeout = 30_000 });

        // The control that used to ask whether to install starters. Its job is the plan's now.
        (await page.Locator("#workflow-install-missing").CountAsync()).ShouldBe(0);
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
