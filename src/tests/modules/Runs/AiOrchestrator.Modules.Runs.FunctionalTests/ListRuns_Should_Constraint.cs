using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// UC-021's API tier: newest-first, the per-Story filter, the empty list, and a response that
/// carries exactly what the Run records — nothing invented for fields no feature produces yet.
/// </summary>
[Collection(RunsCollection.Name)]
public class ListRuns_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        await fixture.ResetDatabase();
        await fixture.ResetQueue();

        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        created.EnsureSuccessStatusCode();
        _projectId = (await created.Content.ReadFromJsonAsync<ProjectResponse>())!.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    Task<List<RunResponse>?> List(string? vendorStoryId = null) =>
        _client.GetFromJsonAsync<List<RunResponse>>(
            vendorStoryId is null
                ? $"/api/projects/{_projectId}/runs"
                : $"/api/projects/{_projectId}/runs?vendorStoryId={vendorStoryId}"
        );

    async Task Seed(
        string vendorStoryId,
        string state,
        DateTimeOffset createdAt,
        decimal? cost = null,
        long? input = null,
        long? output = null
    )
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        await database.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO runs.runs ("Id", "ProjectId", "VendorStoryId", "AutomationId", "State", "CreatedAt", "CostUsd", "UsageInputTokens", "UsageOutputTokens")
            VALUES ({Guid.CreateVersion7()}, {_projectId}, {vendorStoryId}, {Guid.CreateVersion7()}, {state}, {createdAt}, {cost}, {input}, {output})
            """
        );
    }

    [Fact]
    public async Task List_Should_ReturnNewestFirst()
    {
        var origin = DateTimeOffset.UtcNow.AddMinutes(-10);
        await Seed("1", "Queued", origin);
        await Seed("2", "Queued", origin.AddMinutes(1));
        await Seed("3", "Queued", origin.AddMinutes(2));

        var runs = await List();

        runs!.Select(run => run.VendorStoryId).ShouldBe(["3", "2", "1"]);
    }

    [Fact]
    public async Task List_Should_FilterByStory()
    {
        var origin = DateTimeOffset.UtcNow.AddMinutes(-10);
        await Seed("1", "Queued", origin);
        await Seed("2", "Executing", origin.AddMinutes(1));

        var runs = await List(vendorStoryId: "2");

        runs!.Select(run => run.VendorStoryId).ShouldBe(["2"]);
        runs![0].State.ShouldBe("Executing");
    }

    [Fact]
    public async Task List_Should_BeEmptyForAProjectWithNoRuns()
    {
        (await List()).ShouldBeEmpty();
    }

    [Fact]
    public async Task List_Should_ExposeExactlyTheRecordedSubset()
    {
        await Seed("1", "Queued", DateTimeOffset.UtcNow);

        // The raw shape, not the deserialised record: an extra invented field (a cost of 0, an
        // empty logs array) would deserialise away invisibly and ship anyway.
        var raw = await _client.GetStringAsync($"/api/projects/{_projectId}/runs");
        using var parsed = JsonDocument.Parse(raw);
        var fields = parsed
            .RootElement.EnumerateArray()
            .First()
            .EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // outputLink joined in agent-implements-pr; plan/approvedAt/failureReason in
        // approval-gate; dismissedAt in #145; locus/workingFolder/branchName in #210; the change
        // target and instruction in run-on-a-pr — each a deliberate widening, which is why this
        // test exists. It failed on the last two, which is the whole point of asserting a set
        // rather than a subset.
        fields.ShouldBe([
            "approvedAt",
            "automationId",
            "branchName",
            "costUsd",
            "createdAt",
            "dismissedAt",
            "dispatchedAt",
            "failureReason",
            "id",
            "inputTokens",
            "instruction",
            "locus",
            "outputLink",
            "outputTokens",
            "plan",
            "state",
            "targetChangeNumber",
            "targetChangeTitle",
            "targetChangeUrl",
            "vendorStoryId",
            "workingFolder",
        ]);
    }

    [Fact]
    public async Task Cost_Should_SumOnlyReportedRunsAndCountTheRest()
    {
        var origin = DateTimeOffset.UtcNow.AddMinutes(-5);
        await Seed("1", "Succeeded", origin, cost: 0.25m, input: 100, output: 50);
        // A free-model Run: it reported, and what it reported was zero (design D1).
        await Seed("2", "Succeeded", origin, cost: 0m, input: 10, output: 5);
        // Never reported: must not be folded in as zero, or the total lies quietly.
        await Seed("3", "Succeeded", origin, cost: null, input: null, output: null);

        var cost = await _client.GetFromJsonAsync<CostResponse>(
            $"/api/projects/{_projectId}/runs/cost"
        );

        cost!.TotalCostUsd.ShouldBe(0.25m);
        cost.TotalInputTokens.ShouldBe(110);
        cost.ReportedRuns.ShouldBe(2);
        cost.UnknownRuns.ShouldBe(1);
    }

    [Fact]
    public async Task List_Should_DistinguishAZeroCostFromAnUnknownOne()
    {
        var origin = DateTimeOffset.UtcNow.AddMinutes(-5);
        await Seed("free", "Succeeded", origin, cost: 0m, input: 1, output: 1);
        await Seed("silent", "Succeeded", origin, cost: null, input: null, output: null);

        var runs = await List();

        runs!.Single(run => run.VendorStoryId == "free").CostUsd.ShouldBe(0m);
        // The whole point: null, not 0 — "free" and "we were not told" are different facts.
        runs!.Single(run => run.VendorStoryId == "silent").CostUsd.ShouldBeNull();
    }

    sealed record CostResponse(
        decimal TotalCostUsd,
        long TotalInputTokens,
        long TotalOutputTokens,
        int ReportedRuns,
        int UnknownRuns
    );

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record RunResponse(
        Guid Id,
        string VendorStoryId,
        Guid AutomationId,
        string State,
        DateTimeOffset? CreatedAt,
        DateTimeOffset? DispatchedAt,
        decimal? CostUsd
    );
}
