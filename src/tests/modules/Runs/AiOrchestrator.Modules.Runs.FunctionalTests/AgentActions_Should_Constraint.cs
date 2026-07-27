using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// UC-017/018/019: the three actions that touch no code. Each must reach its own vendor write,
/// leave the workspace alone, and fail honestly when the Agent's answer cannot be used — an
/// invented estimate or a guessed state would be worse than no action at all.
/// </summary>
[Collection(RunsCollection.Name)]
public class AgentActions_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
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

        fixture.Vendor.Stories.Add(new VendorStory("9", "A story", "open", [], "Do it."));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 1);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task<Guid> RunWith(string action, string answer, string label)
    {
        var automation = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = label,
                triggerState = (string?)null,
                action,
                runtime = "ClaudeCodeHeadless",
                requiresApproval = false,
            }
        );
        automation.EnsureSuccessStatusCode();
        var automationId = (await automation.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;

        fixture.Agent.Result = new AgentResult(
            Succeeded: true,
            Log: answer,
            OutputLink: null,
            Usage: null
        );

        var run = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "9", automationId }
        );
        run.EnsureSuccessStatusCode();
        var runId = (await run.Content.ReadFromJsonAsync<RunNowResponse>())!.Id;

        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRunExecutor>().Execute(runId);
        return runId;
    }

    async Task<(string State, string? Failure)> Load(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        return await database
            .Runs.Where(run => run.Id == runId)
            .Select(run => new ValueTuple<string, string?>(run.State.ToString(), run.FailureReason))
            .SingleAsync();
    }

    [Fact]
    public async Task RefineOrComment_Should_PostTheAnswerAsAComment()
    {
        var runId = await RunWith("RefineOrComment", "Have you considered the empty case?", "a");

        (await Load(runId)).State.ShouldBe("Succeeded");
        fixture.Vendor.Comments.ShouldContain("Have you considered the empty case?");
        // No code is touched, so no workspace is prepared.
        fixture.Workspace.Published.ShouldBeFalse();
    }

    [Fact]
    public async Task TransitionState_Should_SetTheStateTheAgentNamed()
    {
        var runId = await RunWith("TransitionState", "closed", "b");

        (await Load(runId)).State.ShouldBe("Succeeded");
        fixture.Vendor.Stories.Single(story => story.VendorId == "9").State.ShouldBe("closed");
    }

    [Fact]
    public async Task Estimate_Should_LabelTheStoryAndExplainItself()
    {
        var runId = await RunWith("Estimate", "5 — three files and a migration.", "c");

        (await Load(runId)).State.ShouldBe("Succeeded");
        fixture
            .Vendor.Stories.Single(story => story.VendorId == "9")
            .Labels.ShouldContain("estimate:5");
        // UC-019 asks for the field AND the reasoning.
        fixture.Vendor.Comments.ShouldContain("5 — three files and a migration.");
    }

    [Fact]
    public async Task ARepeatedEstimate_Should_ReplaceTheOldLabelNotAddASecond()
    {
        await RunWith("Estimate", "3 points.", "d");
        // The first Run is terminal, so the Story is free to be estimated again.
        await RunWith("Estimate", "8 points.", "e");

        var labels = fixture.Vendor.Stories.Single(story => story.VendorId == "9").Labels;
        labels.ShouldContain("estimate:8");
        labels.ShouldNotContain("estimate:3");
    }

    [Fact]
    public async Task AnEstimateWithoutANumber_Should_FailRatherThanGuess()
    {
        var runId = await RunWith("Estimate", "It depends on the API.", "f");

        var (state, failure) = await Load(runId);
        state.ShouldBe("Failed");
        failure!.ShouldContain("did not start with a number");
        fixture.Vendor.Stories.Single(story => story.VendorId == "9").Labels.ShouldBeEmpty();
    }

    [Fact]
    public async Task ARejectedState_Should_FailWithTheVendorsReason()
    {
        fixture.Vendor.WriteStateError =
            AiOrchestrator.Modules.Backlog.Domain.BacklogErrors.StateNotAccepted(
                "in-progress",
                "open, closed"
            );

        var runId = await RunWith("TransitionState", "in-progress", "g");

        var (state, failure) = await Load(runId);
        state.ShouldBe("Failed");
        failure!.ShouldContain("in-progress");
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);
}
