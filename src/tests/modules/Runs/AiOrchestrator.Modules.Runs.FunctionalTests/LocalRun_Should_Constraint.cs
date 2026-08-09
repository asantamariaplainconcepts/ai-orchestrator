using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #210 — a folder on the host as a code source, and the locus a Run records. The self-host
/// posture comes from a derived factory carrying <c>Identity:Mode=LocalOwner</c>, so the shared
/// fixture and every existing test keep the posture they always had — which is itself the
/// cloud-absence assertion at the bottom of this class.
/// </summary>
[Collection(RunsCollection.Name)]
public class LocalRun_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    WebApplicationFactory<Program>? _selfHost;
    HttpClient _client = null!;
    Guid _projectId;
    Guid _automationId;
    string _repoPath = string.Empty;

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Agent.Reset();
        fixture.Workspace.Reset();
        await fixture.ResetDatabase();
        await fixture.ResetQueue();

        // The self-host flavour, composed exactly as the product composes it — by the one
        // configuration key — not by faking the check.
        _selfHost = fixture.WithWebHostBuilder(builder =>
            builder.UseSetting("Identity:Mode", "LocalOwner")
        );
        _client = _selfHost.CreateClient();

        // A real repository in a temp folder: the workspace's git plumbing is the subject
        // here, and a fake would prove nothing about it.
        _repoPath = Directory.CreateTempSubdirectory("local-run-").FullName;
        await Git("init", "--initial-branch=main");
        await File.WriteAllTextAsync(Path.Combine(_repoPath, "readme.md"), "hello");
        await Git("add", "--all");
        await Git(
            "-c",
            "user.name=Owner",
            "-c",
            "user.email=owner@example.invalid",
            "commit",
            "-m",
            "seed"
        );

        _projectId = await CreateProject();

        (
            await _client.PutAsJsonAsync(
                $"/api/projects/{_projectId}/connector",
                new
                {
                    owner = "acme",
                    repository = "portal",
                    secretName = "acme-pat",
                    codeSource = "localFolder",
                    localPath = _repoPath,
                }
            )
        ).EnsureSuccessStatusCode();

        _automationId = await CreateAutomation("ai:refine");

        fixture.Vendor.Stories.Add(new VendorStory("1", "Board move misfires", "open", [], "B."));
        await _client.PostAsync($"/api/projects/{_projectId}/backlog/refresh", null);
        await fixture.Probe.WaitForAtLeast(_projectId, 1);
    }

    public Task DisposeAsync()
    {
        _selfHost?.Dispose();
        if (Directory.Exists(_repoPath))
        {
            Directory.Delete(_repoPath, recursive: true);
        }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ValidatePath_Should_NameEachCheck()
    {
        var valid = await Validate(_repoPath);
        valid.IsDirectory.ShouldBeTrue();
        valid.IsGitRepository.ShouldBeTrue();
        valid.Branch.ShouldBe("main");
        valid.IsClean.ShouldBe(true);

        // A directory that is not a repository names exactly that check.
        var plain = Directory.CreateTempSubdirectory("not-a-repo-").FullName;
        try
        {
            var notRepo = await Validate(plain);
            notRepo.IsDirectory.ShouldBeTrue();
            notRepo.IsGitRepository.ShouldBeFalse();
            notRepo.Branch.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(plain, recursive: true);
        }
    }

    [Fact]
    public async Task ALocalRun_Should_DefaultLocal_LeaveABranch_AndPushNothing()
    {
        var runId = await RunNow("1");
        await Execute(runId);

        // The read model carries the audit (BR-014 extended): locus, folder, branch.
        var run = await FindRun(runId);
        run.GetProperty("state").GetString().ShouldBe("Succeeded", $"run: {run.GetRawText()}");
        run.GetProperty("locus").GetString().ShouldBe("Local");
        run.GetProperty("workingFolder").GetString().ShouldBe(_repoPath);
        var branch = run.GetProperty("branchName").GetString();
        branch.ShouldNotBeNull();
        branch.ShouldStartWith("ai/1-");
        run.GetProperty("outputLink").ValueKind.ShouldBe(JsonValueKind.Null);

        // The branch really exists in the owner's repository, and nothing has any remote to
        // have been pushed to — the folder never gained one.
        var branches = await Git("branch", "--list", branch!);
        branches.ShouldContain(branch!);
        (await Git("remote")).Trim().ShouldBeEmpty();

        // The credential promise, where a reader looks for it: the transcript says the host's
        // own credentials were used (design D5) — no vendor token was resolved for this run.
        var log = await _client.GetStringAsync($"/api/projects/{_projectId}/runs/{runId}/log");
        log.ShouldContain("host's own credentials");
    }

    [Fact]
    public async Task ADirtyTree_Should_RefuseBeforeAnyWrite()
    {
        await File.WriteAllTextAsync(Path.Combine(_repoPath, "wip.txt"), "uncommitted");

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "1", automationId = _automationId }
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("uncommitted changes");

        // Refused, not failed: no Run row exists (BR-016's pre-write half).
        var runs = await _client.GetFromJsonAsync<JsonElement>($"/api/projects/{_projectId}/runs");
        runs.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task AnImpossibleLocus_Should_BeRefusedByName()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new
            {
                vendorStoryId = "1",
                automationId = _automationId,
                locus = "Sandbox",
            }
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("sandbox cannot");
    }

    [Fact]
    public async Task ACloudDeployment_Should_HaveNoCodeSourceSurface()
    {
        // The shared fixture's own host — no Identity:Mode — is the cloud posture.
        using var cloud = fixture.CreateClient();

        var validate = await cloud.PostAsJsonAsync(
            $"/api/projects/{_projectId}/connector/validate-path",
            new { path = _repoPath }
        );
        validate.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var configure = await cloud.PutAsJsonAsync(
            $"/api/projects/{_projectId}/connector",
            new
            {
                owner = "acme",
                repository = "portal",
                secretName = "acme-pat",
                codeSource = "localFolder",
                localPath = _repoPath,
            }
        );
        configure.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    async Task<ValidateResponse> Validate(string path)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/connector/validate-path",
            new { path }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ValidateResponse>())!;
    }

    async Task<Guid> RunNow(string story)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = story, automationId = _automationId }
        );
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    async Task Execute(Guid runId)
    {
        // The derived host's own executor: its configuration is the self-host posture.
        await using var scope = _selfHost!.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRunExecutor>().Execute(runId);
    }

    async Task<JsonElement> FindRun(Guid runId)
    {
        var runs = await _client.GetFromJsonAsync<JsonElement>($"/api/projects/{_projectId}/runs");
        return runs.EnumerateArray().Single(run => run.GetProperty("id").GetGuid() == runId);
    }

    async Task<Guid> CreateProject()
    {
        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        created.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    async Task<Guid> CreateAutomation(string trigger)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
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
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    async Task<string> Git(params string[] arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = _repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        process.ExitCode.ShouldBe(0, $"git {string.Join(' ', arguments)}: {stderr}");
        return stdout;
    }

    sealed record ValidateResponse(
        bool IsDirectory,
        bool IsGitRepository,
        string? Branch,
        bool? IsClean
    );
}
