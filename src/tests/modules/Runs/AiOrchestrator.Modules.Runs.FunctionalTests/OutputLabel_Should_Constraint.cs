using System.Net;
using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #115 — chaining stops being the grill's private trick. What must hold: any Automation hands
/// work on when it succeeds, silence stays the default, nothing is handed on when the Run did
/// not succeed, and an Automation cannot feed itself.
/// </summary>
[Collection(RunsCollection.Name)]
public class OutputLabel_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
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

        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        created.EnsureSuccessStatusCode();
        _projectId = (await created.Content.ReadFromJsonAsync<ProjectResponse>())!.Id;

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

        fixture.Vendor.Stories.Add(new VendorStory("7", "A story", "open", [], "Body."));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 1);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task<HttpResponseMessage> CreateAutomation(
        string trigger,
        string action,
        string? outputLabel
    ) =>
        await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = trigger,
                triggerState = (string?)null,
                action,
                runtime = "ClaudeCodeHeadless",
                requiresApproval = false,
                outputLabel,
            }
        );

    async Task<Guid> Automation(string trigger, string action, string? outputLabel)
    {
        var response = await CreateAutomation(trigger, action, outputLabel);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;
    }

    async Task<Guid> Dispatch(Guid automationId)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "7", automationId }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RunNowResponse>())!.Id;
    }

    async Task Execute(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRunExecutor>().Execute(runId);
    }

    async Task<(string State, string? Failure)> Load(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        var run = await database.Runs.SingleAsync(candidate => candidate.Id == runId);
        return (run.State.ToString(), run.FailureReason);
    }

    IReadOnlyList<string> LabelsAtVendor() =>
        fixture.Vendor.Stories.Single(story => story.VendorId == "7").Labels;

    void AgentSays(bool ok, string reply) =>
        fixture.Agent.Result = new AgentResult(ok, reply, null, null);

    [Fact]
    public async Task ASucceedingAutomation_Should_WriteItsOutputLabelAndStartTheNextRun()
    {
        // The point of #115: this is RefineOrComment, not the grill, and it chains.
        var refine = await Automation("ai:refine", "RefineOrComment", "ai:estimate");
        await Automation("ai:estimate", "Estimate", null);

        AgentSays(true, "A helpful comment.");
        var runId = await Dispatch(refine);
        await Execute(runId);

        (await Load(runId)).State.ShouldBe("Succeeded");
        LabelsAtVendor().ShouldContain("ai:estimate");

        // …and ordinary matching carries the baton, exactly as it does for the grill.
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);

        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        var runs = await database.Runs.Where(run => run.ProjectId == _projectId).ToListAsync();

        while (DateTime.UtcNow < deadline && runs.Count < 2)
        {
            await Task.Delay(100);
            runs = await database.Runs.Where(run => run.ProjectId == _projectId).ToListAsync();
        }

        runs.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AnAutomationWithoutAnOutputLabel_Should_EndSilently()
    {
        var refine = await Automation("ai:refine", "RefineOrComment", null);

        AgentSays(true, "A helpful comment.");
        var runId = await Dispatch(refine);
        await Execute(runId);

        (await Load(runId)).State.ShouldBe("Succeeded");
        LabelsAtVendor().ShouldBeEmpty();
    }

    [Fact]
    public async Task AFailingRun_Should_HandNothingOn()
    {
        var refine = await Automation("ai:refine", "RefineOrComment", "ai:estimate");

        AgentSays(false, "boom");
        var runId = await Dispatch(refine);
        await Execute(runId);

        // BR-004: a failed Run is terminal until a human intervenes, so it cannot claim the
        // next step is owed any work.
        (await Load(runId)).State.ShouldBe("Failed");
        LabelsAtVendor().ShouldBeEmpty();
    }

    [Fact]
    public async Task ARefusedLabelWrite_Should_FailTheRunRatherThanClaimSuccess()
    {
        var refine = await Automation("ai:refine", "RefineOrComment", "ai:estimate");
        fixture.Vendor.FailNextLabelWrite = "The vendor rejected the label.";

        AgentSays(true, "A helpful comment.");
        var runId = await Dispatch(refine);
        await Execute(runId);

        // The hand-off is this Automation's deliverable: reporting success while the chain
        // silently stopped is the failure mode this asserts against.
        var (state, failure) = await Load(runId);
        state.ShouldBe("Failed");
        failure.ShouldNotBeNull();
        LabelsAtVendor().ShouldBeEmpty();
    }

    [Fact]
    public async Task AnAutomationFeedingItself_Should_BeRefusedAtSave()
    {
        var response = await CreateAutomation("ai:refine", "RefineOrComment", "ai:refine");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("hand work to itself");
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);
}
