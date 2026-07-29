using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #123 — the fourth step. What must hold: the procedure comes from the repository and never
/// from this product, both refusals land before anything is cloned, and a close that cannot be
/// completed leaves the change exactly as it was.
/// </summary>
[Collection(RunsCollection.Name)]
public class SyncAction_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;
    Guid _automationId;

    const string Procedure = "1. Append a retro entry. 2. Squash-merge with a linted subject.";

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

        _automationId = await Automation("ai:sync", rubricPath: null);

        fixture.Vendor.Stories.Add(new VendorStory("3", "A story", "open", [], "Body."));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 1);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    async Task<Guid> Automation(string trigger, string? rubricPath)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = trigger,
                triggerState = (string?)null,
                action = "SyncChange",
                runtime = "ClaudeCodeHeadless",
                requiresApproval = false,
                rubricPath,
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;
    }

    async Task<Guid> Dispatch(Guid automationId)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "3", automationId }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RunNowResponse>())!.Id;
    }

    async Task Execute(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRunExecutor>().Execute(runId);
    }

    async Task<(string State, string? Failure, string? Output)> Load(Guid runId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        var run = await database.Runs.SingleAsync(candidate => candidate.Id == runId);
        return (run.State.ToString(), run.FailureReason, run.OutputLink);
    }

    /// <summary>The vendor reports the Story's open change; the reader turns it into files.</summary>
    void AnOpenChange() =>
        fixture.Vendor.Change = new LinkedChange(
            41,
            "Story #3",
            "https://github.com/acme/portal/pull/41",
            "story-3"
        );

    [Fact]
    public async Task ASyncRun_Should_FollowTheRepositorysOwnProcedure()
    {
        AnOpenChange();
        fixture.Vendor.Documents["docs/process/close-out.md"] = Procedure;
        fixture.Agent.Result = new AgentResult(true, "Closed as described.", null, null);

        var runId = await Dispatch(_automationId);
        await Execute(runId);

        var (state, _, output) = await Load(runId);
        state.ShouldBe("Succeeded");
        output.ShouldBe("https://github.com/acme/portal/pull/41");

        // The procedure reached the agent verbatim: the product carries no close-out of its own.
        fixture.Agent.Instructions.Single().Prompt.ShouldContain(Procedure);
    }

    [Fact]
    public async Task WithNoOpenChange_Should_RefuseBeforePreparingAWorkspace()
    {
        fixture.Vendor.Documents["docs/process/close-out.md"] = Procedure;

        var runId = await Dispatch(_automationId);
        await Execute(runId);

        var (state, failure, _) = await Load(runId);
        state.ShouldBe("Failed");
        failure.ShouldNotBeNull();
        failure.ShouldContain("no open change");

        // Nothing was cloned and no agent ran: the refusal precedes the spend (design D4).
        fixture.Workspace.Prepared.ShouldBeFalse();
        fixture.Agent.Instructions.ShouldBeEmpty();
    }

    [Fact]
    public async Task WithNoProcedure_Should_RefuseNamingThePath()
    {
        AnOpenChange();

        var runId = await Dispatch(_automationId);
        await Execute(runId);

        var (state, failure, _) = await Load(runId);
        state.ShouldBe("Failed");
        failure.ShouldNotBeNull();
        failure.ShouldContain("docs/process/close-out.md");
        fixture.Workspace.Prepared.ShouldBeFalse();
        fixture.Agent.Instructions.ShouldBeEmpty();
    }

    [Fact]
    public async Task ACustomProcedurePath_Should_BeUsedExactly()
    {
        AnOpenChange();
        fixture.Vendor.Documents["docs/our-own-close-out.md"] = "Merge and tell nobody.";
        var custom = await Automation("ai:sync-custom", "docs/our-own-close-out.md");
        fixture.Agent.Result = new AgentResult(true, "Done.", null, null);

        var runId = await Dispatch(custom);
        await Execute(runId);

        (await Load(runId)).State.ShouldBe("Succeeded");
        fixture.Agent.Instructions.Single().Prompt.ShouldContain("Merge and tell nobody.");
    }

    [Fact]
    public async Task TheAgent_Should_BeToldToLeaveTheChangeAloneOnFailure()
    {
        AnOpenChange();
        fixture.Vendor.Documents["docs/process/close-out.md"] = Procedure;
        fixture.Agent.Result = new AgentResult(true, "Done.", null, null);

        await Execute(await Dispatch(_automationId));

        // Design D5 lives in the prompt, because the runtime is what holds the tools.
        fixture
            .Agent.Instructions.Single()
            .Prompt.ShouldContain("leave the pull request exactly as you found it");
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);
}
