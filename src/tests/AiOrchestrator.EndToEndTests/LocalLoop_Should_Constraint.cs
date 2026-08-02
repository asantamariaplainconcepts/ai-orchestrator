using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.EndToEndTests;

/// <summary>
/// The composition itself is the artifact here (ADR-0004): #50 exists because the dispatch
/// worker was wired without a database and therefore threw at startup for four changes, unseen
/// — the resource required an explicit start and nobody pressed it.
/// <para>
/// Asserting it in the E2E tier rather than by watching a dashboard is the difference between
/// "it worked when someone looked" and a claim CI re-checks on every push.
/// </para>
/// </summary>
[Trait("Category", "E2E")]
[Collection(AppHostCollection.Name)]
public class LocalLoop_Should_Constraint(AppHostFixture fixture)
{
    [Fact]
    public async Task TheLocalHabitat_Should_RunWithoutADispatchWorker()
    {
        // This asserted the worker reached its dependencies, back when the local habitat had one.
        // Since #225 it has none: there is no queue to drain, so the Server consumes the outbox in
        // its own process and the container is gone. What is worth holding is the property that
        // replaced it — the Server itself gets past composition, which is where a wrongly composed
        // dispatch substrate would fail — plus the absence being real rather than assumed.
        await fixture
            .App.ResourceNotifications.WaitForResourceAsync("server", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(2));

        // Named explicitly, because a test that merely stopped mentioning the worker would pass
        // just as well if somebody re-added it and quietly restored the credential boundary this
        // habitat gives up on purpose (DEC-054).
        fixture
            .App.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.Select(resource => resource.Name)
            .ShouldNotContain("dispatch");
    }

    [Fact]
    public async Task TheLocalComposition_Should_SeedADemoProject()
    {
        var page = await fixture.Browser.NewPageAsync();

        var projects = await page.APIRequest.GetAsync($"{fixture.ServerBaseUrl}api/projects");
        projects.Status.ShouldBe(200, await projects.TextAsync());

        // The seeder makes the loop clickable on first boot; the E2E composition is a run
        // composition, so it seeds exactly as a developer's would.
        (await projects.TextAsync()).ShouldContain("Demo project");
    }
}
