using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.ServiceDefaults.Dispatch;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #225 — the queueless habitat. What is worth proving is not that a registration exists but that
/// a Run **executes** with no queue: the portal publishes to the outbox, the subscriber in the
/// same process consumes it, and the lifecycle is the one the queue path produces.
/// <para>
/// Composed by the one configuration key the product composes on — a blank queue connection
/// string — rather than by faking the branch, so what passes here is what a self-hoster runs.
/// </para>
/// </summary>
[Collection(RunsCollection.Name)]
public class OutboxDispatch_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    WebApplicationFactory<Program>? _queueless;
    HttpClient _client = null!;
    Guid _projectId;
    Guid _automationId;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Agent.Reset();
        fixture.Workspace.Reset();
        await fixture.ResetDatabase();
        await fixture.ResetQueue();

        // The shared fixture removes the outbox consumer so background execution cannot race
        // tests that drive the executor by hand — but the consumer is exactly what THIS test
        // proves, so it alone puts it back.
        _queueless = fixture.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<OutboxRunSubscriber>())
        );
        _client = _queueless.CreateClient();

        _projectId = await CreateProject();
        (
            await _client.PutAsJsonAsync(
                $"/api/projects/{_projectId}/connector",
                new
                {
                    owner = "acme",
                    repository = "portal",
                    secretName = "acme-pat",
                }
            )
        ).EnsureSuccessStatusCode();

        _automationId = await CreateAutomation("ai:refine");

        fixture.Vendor.Stories.Add(new VendorStory("1", "A Story", "open", [], "Body."));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 1);
    }

    public Task DisposeAsync()
    {
        _queueless?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ARun_Should_ExecuteWithNoQueue()
    {
        var dispatched = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "1", automationId = _automationId }
        );
        dispatched.EnsureSuccessStatusCode();

        var state = await Eventually(run =>
            new[] { "Succeeded", "Failed", "Cancelled" }.Contains(
                run.GetProperty("state").GetString()
            )
        );

        // Terminal by the same path a queued Run takes — the substrate changed, the lifecycle
        // did not (the spec's indistinguishable-lifecycle scenario).
        state.ShouldBe("Succeeded");

        // And the dispatch is durably in the outbox — the one substrate there is since #296.
        (await fixture.DispatchedRunIds()).ShouldNotBeEmpty();
    }

    async Task<string?> Eventually(Func<JsonElement, bool> done)
    {
        // The agent is faked, so this settles in milliseconds; the budget is for CI's slower disk
        // rather than for anything this test is waiting on by design.
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var runs = await _client.GetFromJsonAsync<JsonElement>(
                $"/api/projects/{_projectId}/runs"
            );

            if (runs.GetArrayLength() > 0 && done(runs[0]))
            {
                return runs[0].GetProperty("state").GetString();
            }

            await Task.Delay(100);
        }

        return null;
    }

    async Task<Guid> CreateProject()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"Outbox {Guid.NewGuid():N}" }
        );
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    async Task<Guid> CreateAutomation(string trigger)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = trigger,
                action = "RepositoryPrompt",
                runtime = "OpenCode",
                requiresApproval = false,
                // The name the fixture seeds a body for, so this test is about the substrate
                // rather than about a missing prompt file.
                promptPath = "story.md",
            }
        );
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }
}
