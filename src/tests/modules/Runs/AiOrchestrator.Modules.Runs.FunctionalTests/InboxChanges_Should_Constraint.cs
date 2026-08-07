using System.Net.Http.Json;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// inbox-open-prs — the review queue beside the Run waits. What must hold: the changes arrive
/// newest first from the vendor and are never stored; the product's own change carries its Run,
/// joined from what the Runs already record (BR-014's output link), never asked of the vendor;
/// and one project's refusal degrades to its reason without blanking anybody else's rows.
/// </summary>
[Collection(RunsCollection.Name)]
public class InboxChanges_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Agent.Reset();
        fixture.Workspace.Reset();
        await fixture.ResetDatabase();
        await fixture.ResetQueue();

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
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task OpenChanges_Should_ArriveNewestFirstAndNeverBeStored()
    {
        fixture.Vendor.Open.Add(
            new OpenChange(
                7,
                "older",
                "https://github.com/acme/portal/pull/7",
                "feature/older",
                DateTimeOffset.UtcNow.AddDays(-2)
            )
        );
        fixture.Vendor.Open.Add(
            new OpenChange(
                9,
                "newer",
                "https://github.com/acme/portal/pull/9",
                "feature/newer",
                DateTimeOffset.UtcNow.AddHours(-1)
            )
        );

        var response = await Read();

        response.Changes.Select(change => change.Number).ShouldBe([9, 7]);
        response.Refusals.ShouldBeEmpty();

        // The vendor is the truth (BR-008): a change gone from the vendor is gone on the next
        // read, which is only possible because nothing about it was stored.
        fixture.Vendor.Open.RemoveAll(change => change.Number == 9);
        (await Read()).Changes.Select(change => change.Number).ShouldBe([7]);
    }

    [Fact]
    public async Task AProductCreatedChange_Should_CarryItsRun()
    {
        // Seeded the way production actually writes it (ADR-0013 — the first version of this
        // test seeded OutputLink by hand, a column the retired publish step (DEC-062) means
        // nothing writes any more, so the marker it proved could never fire for real). What the
        // ceremony does own is the branch: run/<id> carries the Run's id.
        var runId = await SeedRun();

        fixture.Vendor.Open.Add(
            new OpenChange(
                1,
                "the product's own work",
                "https://github.com/acme/portal/pull/1",
                $"run/{runId}",
                DateTimeOffset.UtcNow
            )
        );

        var entry = (await Read()).Changes.ShouldHaveSingleItem();
        entry.RunId.ShouldBe(runId);
    }

    [Fact]
    public async Task ABranchThatMerelyLooksLikeTheCeremonys_Should_ClaimNoRun()
    {
        // A run/<guid> branch whose guid is no Run of this project must not be marked — the
        // branch pattern alone is a claim anybody can make with a git push.
        fixture.Vendor.Open.Add(
            new OpenChange(
                2,
                "an impostor branch",
                "https://github.com/acme/portal/pull/2",
                $"run/{Guid.NewGuid()}",
                DateTimeOffset.UtcNow
            )
        );

        var entry = (await Read()).Changes.ShouldHaveSingleItem();
        entry.RunId.ShouldBeNull();
    }

    [Fact]
    public async Task ARefusedVendor_Should_DegradeToItsReasonBesideNothing()
    {
        fixture.Vendor.OpenChangesError = BacklogErrors.VendorUnavailable("rate limited");

        var response = await Read();

        response.Changes.ShouldBeEmpty();
        var refusal = response.Refusals.ShouldHaveSingleItem();
        refusal.ProjectId.ShouldBe(_projectId);
        refusal.Reason.ShouldContain("rate limited");
    }

    [Fact]
    public async Task AProjectWithoutAConnector_Should_ContributeNothing()
    {
        // A second project, never connected: no entries, and no refusal either — nothing to
        // list is a state, not a failure.
        var bare = await CreateProject();

        var response = await Read();

        response.Changes.ShouldBeEmpty();
        response.Refusals.ShouldNotContain(refusal => refusal.ProjectId == bare);
    }

    async Task<InboxChangesResponse> Read()
    {
        var response = await _client.GetAsync("/api/inbox/changes");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InboxChangesResponse>())!;
    }

    async Task<Guid> CreateProject()
    {
        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        created.EnsureSuccessStatusCode();
        var project = (await created.Content.ReadFromJsonAsync<ProjectResponse>())!;
        return project.Id;
    }

    /// <summary>An ordinary story Run, created through the API — the id is what the test needs.</summary>
    async Task<Guid> SeedRun()
    {
        await CreateAutomation(_projectId, "ai:implement");
        fixture.Vendor.Stories.Add(new("41", "A story", "open", [], "Body."));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 1);

        var created = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "41", automationId = _automationId }
        );
        created.EnsureSuccessStatusCode();
        var run = (await created.Content.ReadFromJsonAsync<RunCreatedResponse>())!;
        return run.Id;
    }

    Guid _automationId;

    async Task CreateAutomation(Guid projectId, string trigger)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/automations",
            new
            {
                triggerLabel = trigger,
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "story.md",
                requiresApproval = false,
            }
        );
        response.EnsureSuccessStatusCode();
        var automation = (await response.Content.ReadFromJsonAsync<AutomationResponse>())!;
        _automationId = automation.Id;
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunCreatedResponse(Guid Id);

    sealed record InboxChangesResponse(
        IReadOnlyList<ChangeEntry> Changes,
        IReadOnlyList<RefusalEntry> Refusals
    );

    sealed record ChangeEntry(
        Guid ProjectId,
        string? ProjectName,
        int Number,
        string Title,
        string Url,
        DateTimeOffset CreatedAt,
        Guid? RunId
    );

    sealed record RefusalEntry(Guid ProjectId, string? ProjectName, string Reason);
}
