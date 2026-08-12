using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.ServiceDefaults.Agents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// #332 — the Admin-configured command that makes a Local Run's fresh checkout buildable, run
/// before the Agent starts. A real repository and a real shell: the subject is whether an actual
/// command actually runs in the actual checkout, and a fake would prove nothing about it. Only the
/// overrun path is scripted, because the smallest timeout an Admin may configure is one minute and
/// a test must not spend one (the real kill-on-timeout is
/// <see cref="LocalCheckoutSetupRunner_Should_Constraint"/>'s subject).
/// </summary>
[Collection(RunsCollection.Name)]
public class CheckoutSetup_Should_Constraint(RunsApiFixture fixture) : IAsyncLifetime
{
    WebApplicationFactory<Program>? _selfHost;
    HttpClient _client = null!;
    Guid _projectId;
    Guid _automationId;
    string _repoPath = string.Empty;
    readonly ScriptedCheckoutSetup _setup = new();

    public async Task InitializeAsync()
    {
        fixture.Vendor.Reset();
        fixture.Agent.Reset();
        fixture.Workspace.Reset();
        await fixture.ResetDatabase();
        await fixture.ResetQueue();
        _setup.Reset();

        _selfHost = fixture.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Identity:Mode", "LocalOwner");
            // Real by default — the fake only stands in front of the real runner when a test
            // scripts the overrun, so every other assertion here runs a genuine shell.
            builder.ConfigureTestServices(services =>
                services.AddSingleton<ILocalCheckoutSetup>(_setup)
            );
        });
        _client = _selfHost.CreateClient();

        _repoPath = Directory.CreateTempSubdirectory("setup-run-").FullName;
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

        // A Connector before the refresh: the mirror has nothing to fill without one, and each
        // test then reconfigures the same Connector with the command it is about.
        await Configure(setupCommand: null);

        _automationId = await CreateAutomation();

        fixture.Vendor.Stories.Add(new VendorStory("1", "Make the tests pass", "open", [], "B."));
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

    /// <summary>
    /// Criterion 1 — and the *ordering* is the assertion, not merely that the command ran. The
    /// marker is looked for from inside the Agent's own invocation, in the very directory the Agent
    /// was handed: a setup that ran afterwards, or in some other folder, fails this.
    /// </summary>
    [Fact]
    public async Task AConfiguredCommand_Should_RunInTheRunsCheckout_BeforeTheAgent()
    {
        await Configure("echo prepared > setup-marker.txt");

        var preparedWhenTheAgentRan = false;
        fixture.Agent.OnExecute = () =>
        {
            var workspace = fixture.Agent.Instructions[^1].WorkspacePath;
            preparedWhenTheAgentRan = File.Exists(Path.Combine(workspace, "setup-marker.txt"));
            return Task.CompletedTask;
        };

        var runId = await RunNow();
        await Execute(runId);

        (await FindRun(runId)).GetProperty("state").GetString().ShouldBe("Succeeded");
        preparedWhenTheAgentRan.ShouldBeTrue(
            "the agent must meet a checkout the setup command has already prepared"
        );

        // In the Run's own checkout, never the owner's folder (#331 is what this stands on).
        File.Exists(Path.Combine(_repoPath, "setup-marker.txt")).ShouldBeFalse();
    }

    /// <summary>
    /// Criterion 2 — a named refusal, before any agent spend, tellable from an agent's own failure.
    /// </summary>
    [Fact]
    public async Task AFailingCommand_Should_EndTheRunByName_BeforeTheAgentRuns()
    {
        await Configure("echo 'dependency resolution failed' && exit 2");

        var runId = await RunNow();
        await Execute(runId);

        var run = await FindRun(runId);
        run.GetProperty("state").GetString().ShouldBe("Failed");

        var reason = run.GetProperty("failureReason").GetString();
        reason.ShouldNotBeNull();
        // It says it was the SETUP: a reader must know whether to fix their build or their Story.
        reason.ShouldContain("setup command");
        // The command as configured, and the tail of what it said — the evidence BR-004 needs,
        // because nothing retries and whoever reads this is the retry.
        reason.ShouldContain("exit 2");
        reason.ShouldContain("dependency resolution failed");

        // Nothing was spent on an agent that could not have worked.
        fixture.Agent.Instructions.ShouldBeEmpty();
    }

    /// <summary>Criterion 3 — absence is a configuration, not a fault.</summary>
    [Fact]
    public async Task NoCommandConfigured_Should_StartNothingAndRunTheAgentImmediately()
    {
        await Configure(setupCommand: null);

        var runId = await RunNow();
        await Execute(runId);

        (await FindRun(runId)).GetProperty("state").GetString().ShouldBe("Succeeded");
        fixture.Agent.Instructions.Count.ShouldBe(1);
        _setup.Calls.ShouldBe(0, "no command configured means no process is started at all");

        var log = await _client.GetStringAsync($"/api/projects/{_projectId}/runs/{runId}/log");
        log.ShouldNotContain("Preparing the checkout");
    }

    /// <summary>
    /// Criterion 4, first half — the overrun names the LIMIT, not the command. A Run that ran out
    /// of time did not fail its build, and a reason claiming it did would send its reader to the
    /// wrong repository.
    /// </summary>
    [Fact]
    public async Task AnOverrunningSetup_Should_NameTheLimit_NotASetupFailure()
    {
        await Configure("a-command-that-would-take-too-long");
        _setup.TimeOutInstead = true;

        var runId = await RunNow();
        await Execute(runId);

        var run = await FindRun(runId);
        run.GetProperty("state").GetString().ShouldBe("Failed");

        var reason = run.GetProperty("failureReason").GetString();
        reason.ShouldNotBeNull();
        reason.ShouldContain("timeout");
        reason.ShouldNotContain("Its output ended with");

        fixture.Agent.Instructions.ShouldBeEmpty();
    }

    /// <summary>
    /// Criterion 4, second half — one budget, shared. The agent is invoked with what setup left,
    /// which is the whole of why setup gets no clock of its own (BR-005, DEC-054's ceiling).
    /// </summary>
    [Fact]
    public async Task TheAgent_Should_BeInvokedWithWhatSetupDidNotSpend()
    {
        // Real, and deliberately slow enough to be measurable against a 30-minute budget without
        // making the suite wait: two seconds is far outside any clock jitter.
        await Configure("sleep 2");

        var runId = await RunNow();
        await Execute(runId);

        (await FindRun(runId)).GetProperty("state").GetString().ShouldBe("Succeeded");

        var granted = fixture.Agent.Instructions.Single().Timeout;
        granted.ShouldBeLessThan(
            PhaseBudget.Default - TimeSpan.FromSeconds(1),
            "setup spends the phase's budget, so the agent cannot receive the whole of it again"
        );
        granted.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    /// <summary>
    /// Criterion 5 — one log, in the order the work happened, with the command named before it
    /// runs so a setup that hangs is legible while it hangs (UC-027).
    /// </summary>
    [Fact]
    public async Task TheSetupsOutput_Should_PrecedeTheAgentsInTheSameLog()
    {
        await Configure("echo installing-dependencies");
        fixture.Agent.Result = new AgentResult(
            Succeeded: true,
            Log: "agent-started-here",
            OutputLink: null,
            Usage: null
        );

        var runId = await RunNow();
        await Execute(runId);

        var log = await _client.GetStringAsync($"/api/projects/{_projectId}/runs/{runId}/log");

        var header = log.IndexOf("Preparing the checkout", StringComparison.Ordinal);
        var setupOutput = log.IndexOf("installing-dependencies", StringComparison.Ordinal);
        var agentOutput = log.IndexOf("agent-started-here", StringComparison.Ordinal);

        header.ShouldBeGreaterThanOrEqualTo(0, $"log: {log}");
        setupOutput.ShouldBeGreaterThan(header, "the command is named before it runs");
        agentOutput.ShouldBeGreaterThan(setupOutput, "setup's output precedes the agent's");
    }

    /// <summary>
    /// The command comes from the Connector and from nowhere else. A file in the checkout naming
    /// setup steps is UC-031's capability, with a per-version trust ceremony this one does not have
    /// — and on this lane the checkout is what the agent edits.
    /// </summary>
    [Fact]
    public async Task AFileInTheCheckout_Should_NeitherBeReadNorExecutedAsSetup()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_repoPath, "setup.sh"),
            "#!/bin/sh\necho from-the-repository > repo-setup-ran.txt\n"
        );
        await Git("add", "--all");
        await Git(
            "-c",
            "user.name=Owner",
            "-c",
            "user.email=owner@example.invalid",
            "commit",
            "-m",
            "a setup file the product must ignore"
        );

        await Configure(setupCommand: null);

        var ranFromTheRepository = false;
        fixture.Agent.OnExecute = () =>
        {
            var workspace = fixture.Agent.Instructions[^1].WorkspacePath;
            ranFromTheRepository = File.Exists(Path.Combine(workspace, "repo-setup-ran.txt"));
            return Task.CompletedTask;
        };

        var runId = await RunNow();
        await Execute(runId);

        ranFromTheRepository.ShouldBeFalse();
        _setup.Calls.ShouldBe(0);
    }

    async Task Configure(string? setupCommand) =>
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
                    localSetupCommand = setupCommand,
                }
            )
        ).EnsureSuccessStatusCode();

    async Task<Guid> CreateProject()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new { name = "Portal" });
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    async Task<Guid> CreateAutomation()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "ai:refine",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "story.md",
            }
        );
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    async Task<Guid> RunNow()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/runs",
            new { vendorStoryId = "1", automationId = _automationId }
        );
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    async Task Execute(Guid runId)
    {
        await using var scope = _selfHost!.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRunExecutor>().Execute(runId);
    }

    async Task<JsonElement> FindRun(Guid runId)
    {
        var runs = await _client.GetFromJsonAsync<JsonElement>($"/api/projects/{_projectId}/runs");
        return runs.EnumerateArray().Single(run => run.GetProperty("id").GetGuid() == runId);
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
}

/// <summary>
/// The real runner, with one scripted door: <see cref="TimeOutInstead"/> reports BR-005's outcome
/// without spending the minute an Admin's smallest configurable timeout would cost. Everything else
/// goes to a genuine shell, because "did an actual command actually run in the actual checkout" is
/// the thing these tests exist to answer.
/// </summary>
sealed class ScriptedCheckoutSetup : ILocalCheckoutSetup
{
    readonly LocalCheckoutSetupRunner _real = new();

    public bool TimeOutInstead { get; set; }

    public int Calls { get; private set; }

    public void Reset()
    {
        TimeOutInstead = false;
        Calls = 0;
    }

    public Task<LocalSetupOutcome> Run(
        string commandLine,
        string workingDirectory,
        TimeSpan budget,
        Action<string> onOutput,
        CancellationToken cancellationToken = default
    )
    {
        Calls++;

        return TimeOutInstead
            ? Task.FromResult(
                new LocalSetupOutcome(TimedOut: true, ExitCode: -1, Output: string.Empty)
            )
            : _real.Run(commandLine, workingDirectory, budget, onOutput, cancellationToken);
    }
}
