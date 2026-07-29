using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #145 — the decision UC-026 could not express. What must hold: a dismissed failure leaves the
/// inbox <b>and</b> every count of failures awaiting a decision, the Run stays `Failed`, and only a
/// failure can be dismissed.
/// <para>
/// The list and the count are asserted together on purpose. Each used to hold its own copy of the
/// predicate under a comment promising they would not disagree; adding the dismissal to one and not
/// the other is exactly how a Member ends up reading "1 waiting" above an empty page.
/// </para>
/// </summary>
[Collection(RunsCollection.Name)]
public class DismissedFailure_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;
    Guid _automationId;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Agent.Reset();
        await fixture.ResetDatabase();
        await fixture.ResetQueue();

        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        created.EnsureSuccessStatusCode();
        _projectId = (await created.Content.ReadFromJsonAsync<ProjectResponse>())!.Id;

        var automation = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "ai:refine",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                rubricPath = "task.md",
                runtime = "ClaudeCodeHeadless",
                requiresApproval = false,
            }
        );
        automation.EnsureSuccessStatusCode();
        _automationId = (await automation.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task<Guid> AFailedRun(string storyId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();

        var run = Run.Create(_projectId, storyId, _automationId, DateTimeOffset.UtcNow);
        run.MarkExecuting(DateTimeOffset.UtcNow);
        run.Fail(DateTimeOffset.UtcNow, "the vendor refused");

        database.Runs.Add(run);
        await database.SaveChangesAsync();
        return run.Id;
    }

    async Task<int> InboxFailures() =>
        (await _client.GetFromJsonAsync<List<InboxEntry>>("/api/inbox"))!.Count(entry =>
            entry.WaitingFor == "failure"
        );

    async Task<int> PulseFailures() =>
        (await _client.GetFromJsonAsync<PulseResponse>($"/api/projects/{_projectId}/pulse"))!
            .Waiting
            .Failure;

    [Fact]
    public async Task ADismissedFailure_Should_LeaveBothTheListAndTheCount()
    {
        var runId = await AFailedRun("21");

        // Both agree it waits, before anything is decided.
        (await InboxFailures()).ShouldBe(1);
        (await PulseFailures()).ShouldBe(1);

        (
            await _client.PostAsync($"/api/projects/{_projectId}/runs/{runId}/dismiss", null)
        ).EnsureSuccessStatusCode();

        // And both agree it does not. One shared predicate, so they cannot answer differently.
        (await InboxFailures()).ShouldBe(0);
        (await PulseFailures()).ShouldBe(0);
    }

    [Fact]
    public async Task ADismissedRun_Should_StayFailedAndRecordWhen()
    {
        var runId = await AFailedRun("22");

        (
            await _client.PostAsync($"/api/projects/{_projectId}/runs/{runId}/dismiss", null)
        ).EnsureSuccessStatusCode();

        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        var run = await database.Runs.AsNoTracking().SingleAsync(entity => entity.Id == runId);

        // A dismissal says what a person decided, never what happened (BR-014).
        run.State.ShouldBe(RunState.Failed);
        run.DismissedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task DismissingTwice_Should_KeepTheFirstDecisionsTime()
    {
        var runId = await AFailedRun("23");
        var url = $"/api/projects/{_projectId}/runs/{runId}/dismiss";

        (await _client.PostAsync(url, null)).EnsureSuccessStatusCode();
        var first = await Dismissed(runId);

        (await _client.PostAsync(url, null)).EnsureSuccessStatusCode();

        // Idempotent: a second press is not a second decision.
        (await Dismissed(runId)).ShouldBe(first);
    }

    [Fact]
    public async Task DismissingARunThatIsNotAFailure_Should_BeRefusedNamingTheState()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        var run = Run.Create(_projectId, "24", _automationId, DateTimeOffset.UtcNow);
        run.MarkExecuting(DateTimeOffset.UtcNow);
        database.Runs.Add(run);
        await database.SaveChangesAsync();

        var response = await _client.PostAsync(
            $"/api/projects/{_projectId}/runs/{run.Id}/dismiss",
            null
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Executing");
    }

    async Task<DateTimeOffset?> Dismissed(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        return (
            await database.Runs.AsNoTracking().SingleAsync(entity => entity.Id == runId)
        ).DismissedAt;
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    // The field is WaitingFor and its value is "failure" — read from GetInbox rather than guessed,
    // after guessing "Reason" and getting a null back.
    sealed record InboxEntry(Guid RunId, string WaitingFor);

    sealed record PulseWaiting(int Approval, int Input, int Failure);

    sealed record PulseResponse(PulseWaiting Waiting);
}
