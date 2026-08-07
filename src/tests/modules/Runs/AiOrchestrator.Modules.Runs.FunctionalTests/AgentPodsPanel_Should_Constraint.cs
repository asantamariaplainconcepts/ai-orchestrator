using System.Net.Http.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Dispatch;
using AiOrchestrator.Modules.Backlog.Connectors;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// Design review 5b — the pods panel's read. The properties worth holding: an unhosted habitat
/// says so rather than rendering an empty machine; a sighting joins its Run's Story, trigger
/// and runtime; and a sighting the database cannot explain is omitted rather than rendered
/// blank.
/// </summary>
[Collection(RunsCollection.Name)]
public class AgentPodsPanel_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;
    string _projectName = "";
    Guid _automationId;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Agent.Reset();
        fixture.Workspace.Reset();
        fixture.Pods.Reset();
        fixture.Runtimes.Reset();
        await fixture.ResetDatabase();
        await fixture.ResetQueue();

        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        created.EnsureSuccessStatusCode();
        var project = (await created.Content.ReadFromJsonAsync<ProjectResponse>())!;
        _projectId = project.Id;
        _projectName = project.Name;

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

        var automation = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "ai:implement",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "story.md",
                requiresApproval = false,
            }
        );
        automation.EnsureSuccessStatusCode();
        _automationId = (await automation.Content.ReadFromJsonAsync<AutomationResponse>())!.Id;

        // The Run a sighting will point at needs its Story mirrored first, exactly as Run now
        // requires everywhere else.
        fixture.Vendor.Stories.Add(new VendorStory("7", "Sighted story", "open", []));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 1);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AnUnhostedHabitat_Should_SaySoInsteadOfShowingAnEmptyMachine()
    {
        var view = (await _client.GetFromJsonAsync<PodsResponse>("/api/pods"))!;

        view.Hosted.ShouldBeFalse();
        view.Pods.ShouldBeEmpty();
    }

    [Fact]
    public async Task ASighting_Should_JoinItsRunsStoryTriggerAndRuntime()
    {
        var run = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "7", automationId = _automationId }
        );
        run.EnsureSuccessStatusCode();
        var runId = (await run.Content.ReadFromJsonAsync<RunNowResponse>())!.Id;

        fixture.Pods.Next = new AgentPodsSnapshot(
            Hosted: true,
            DockerReady: true,
            ImagePresent: true,
            CheckedAt: DateTimeOffset.UtcNow,
            ProbeInterval: TimeSpan.FromSeconds(30),
            MaxConcurrentPods: 2,
            Pods: [new AgentPodSighting(runId, Executing: true, DateTimeOffset.UtcNow)]
        );

        var view = (await _client.GetFromJsonAsync<PodsResponse>("/api/pods"))!;

        view.Hosted.ShouldBeTrue();
        view.DockerReady.ShouldBeTrue();
        view.RetrySeconds.ShouldBe(30);
        view.MaxConcurrentPods.ShouldBe(2);
        var pod = view.Pods.ShouldHaveSingleItem();
        pod.RunId.ShouldBe(runId);
        pod.ProjectId.ShouldBe(_projectId);
        pod.ProjectName.ShouldBe(_projectName);
        pod.VendorStoryId.ShouldBe("7");
        pod.TriggerLabel.ShouldBe("ai:implement");
        pod.Runtime.ShouldBe("ClaudeCodeHeadless");
        pod.Executing.ShouldBeTrue();
    }

    [Fact]
    public async Task ASightingWithoutARun_Should_BeOmittedNotRenderedBlank()
    {
        fixture.Pods.Next = new AgentPodsSnapshot(
            Hosted: true,
            DockerReady: false,
            ImagePresent: null,
            CheckedAt: DateTimeOffset.UtcNow,
            ProbeInterval: TimeSpan.FromSeconds(30),
            MaxConcurrentPods: 1,
            Pods: [new AgentPodSighting(Guid.NewGuid(), Executing: false, DateTimeOffset.UtcNow)]
        );

        var view = (await _client.GetFromJsonAsync<PodsResponse>("/api/pods"))!;

        // The machine's facts still travel — a row nobody can explain does not.
        view.Hosted.ShouldBeTrue();
        view.DockerReady.ShouldBeFalse();
        view.ImagePresent.ShouldBeNull();
        view.Pods.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheRuntimes_Should_TravelWithThePanelAndCarryTheirRemedy()
    {
        // #279 — the runtimes' readiness rides the same read, remedies attached: a missing CLI
        // names its install command, a switched-off credential is null (a different sentence
        // from "does not resolve"), and the cadence restates the probe's behaviour.
        fixture.Runtimes.Next = new AgentRuntimesSnapshot(
            Hosted: true,
            CheckedAt: DateTimeOffset.UtcNow,
            ProbeInterval: TimeSpan.FromSeconds(30),
            Runtimes:
            [
                new AgentRuntimeState(
                    Name: "OpenCode",
                    Command: "opencode",
                    CliReady: false,
                    InstallCommand: AgentRuntimeRemedies.InstallOpenCode,
                    CredentialSecretName: null,
                    CredentialReady: null
                ),
                new AgentRuntimeState(
                    Name: "ClaudeCodeHeadless",
                    Command: "claude",
                    CliReady: true,
                    InstallCommand: AgentRuntimeRemedies.InstallClaudeCode,
                    CredentialSecretName: "anthropic-api-key",
                    CredentialReady: false
                ),
            ]
        );

        var view = (await _client.GetFromJsonAsync<PodsResponse>("/api/pods"))!;

        view.Runtimes.Hosted.ShouldBeTrue();
        view.Runtimes.RetrySeconds.ShouldBe(30);
        var opencode = view.Runtimes.Runtimes.Single(r => r.Name == "OpenCode");
        opencode.CliReady.ShouldBeFalse();
        opencode.InstallCommand.ShouldBe(AgentRuntimeRemedies.InstallOpenCode);
        opencode.CredentialReady.ShouldBeNull();
        var claude = view.Runtimes.Runtimes.Single(r => r.Name == "ClaudeCodeHeadless");
        claude.CliReady.ShouldBeTrue();
        claude.CredentialSecretName.ShouldBe("anthropic-api-key");
        claude.CredentialReady.ShouldBe(false);
    }

    [Fact]
    public async Task AnUnhostedRuntimesProcess_Should_SaySo()
    {
        // "These runtimes are not ready here" and "Runs execute somewhere this process cannot
        // see" are different sentences; the default is the second.
        var view = (await _client.GetFromJsonAsync<PodsResponse>("/api/pods"))!;

        view.Runtimes.Hosted.ShouldBeFalse();
        view.Runtimes.Runtimes.ShouldBeEmpty();
    }

    sealed record ProjectResponse(Guid Id, string Name);

    sealed record AutomationResponse(Guid Id);

    sealed record RunNowResponse(Guid Id);

    sealed record PodsResponse(
        bool Hosted,
        bool DockerReady,
        bool? ImagePresent,
        DateTimeOffset? CheckedAt,
        int RetrySeconds,
        int MaxConcurrentPods,
        IReadOnlyList<PodEntry> Pods,
        RuntimesEntry Runtimes
    );

    sealed record RuntimesEntry(
        bool Hosted,
        DateTimeOffset? CheckedAt,
        int RetrySeconds,
        IReadOnlyList<RuntimeEntry> Runtimes
    );

    sealed record RuntimeEntry(
        string Name,
        string Command,
        bool CliReady,
        string InstallCommand,
        string? CredentialSecretName,
        bool? CredentialReady
    );

    sealed record PodEntry(
        Guid RunId,
        Guid ProjectId,
        string? ProjectName,
        string VendorStoryId,
        string? TriggerLabel,
        string? Runtime,
        bool Executing,
        DateTimeOffset SightedAt
    );
}
