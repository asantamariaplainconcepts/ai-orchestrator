using Aspire.Hosting.ApplicationModel;
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
[Collection(AppHostCollection.Name)]
public class LocalLoop_Should_Constraint(AppHostFixture fixture)
{
    [Fact]
    public async Task TheDispatchWorker_Should_StartAndReachItsDependencies()
    {
        // Running means the process got past composition — which is exactly what it could not
        // do while the AppHost gave it a queue and no database.
        await fixture
            .App.ResourceNotifications.WaitForResourceAsync("dispatch", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(2));
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
