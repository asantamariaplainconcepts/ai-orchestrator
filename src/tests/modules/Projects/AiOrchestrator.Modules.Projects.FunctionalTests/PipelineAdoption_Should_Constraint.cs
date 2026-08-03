using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.FunctionalTests;

/// <summary>
/// #229 — setting a project up adopts the pipeline the repository already has. The property that
/// matters is that <b>nothing is duplicated and nothing is guessed</b>: a repository carrying
/// <c>.claude/commands/ds/grill.md</c> gets an Automation naming that file, a file matching no step
/// is reported rather than interpreted, and a starter is written only where there is no file at all.
/// </summary>
[Collection(ProjectsCollection.Name)]
public class PipelineAdoption_Should_Constraint(ProjectsApiFixture fixture) : IAsyncLifetime
{
    readonly HttpClient _client = fixture.CreateClient();
    Guid _projectId;

    public async Task InitializeAsync()
    {
        await fixture.ResetDatabase();

        var created = await _client.PostAsJsonAsync(
            "/api/projects",
            new { name = $"p-{Guid.NewGuid():N}" }
        );
        created.EnsureSuccessStatusCode();
        _projectId = (await created.Content.ReadFromJsonAsync<ProjectRef>())!.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>The motivating repository: a pipeline kept one level under `.claude/commands`.</summary>
    void ArrangeDsConnect()
    {
        fixture.Documents.Directories[".claude/commands"] = StubDirectory.Holding("ds");
        fixture.Documents.Directories[".claude/commands/ds"] = StubDirectory.Of(
            "grill.md",
            "propose.md",
            "implement.md",
            "sync.md",
            "sprint-notes.md"
        );
    }

    [Fact]
    public async Task Discovery_Should_FindAPipelineKeptOneLevelDown()
    {
        ArrangeDsConnect();

        var found = await Discover();

        var candidate = found
            .GetProperty("candidates")
            .EnumerateArray()
            .Single(entry => entry.GetProperty("directory").GetString() == ".claude/commands/ds");

        // The names are the mapping (design D3) — and `.claude/commands` itself, which holds only
        // a subdirectory, is not offered as a candidate because it holds no prompt file.
        Strings(candidate, "steps")
            .ShouldBe(["ai:implement", "ai:grill", "ai:propose", "ai:sync"], ignoreOrder: true);
        Strings(candidate, "unmatched").ShouldBe(["sprint-notes.md"]);

        // Nothing was written: discovery proposes and never picks (design D1).
        fixture.Directories.Saved.ShouldBeEmpty();
        (await Automations()).GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task TwoCandidates_Should_BothBeOfferedAndNeitherChosen()
    {
        ArrangeDsConnect();
        fixture.Documents.Directories["ai/prompts"] = StubDirectory.Of("triage.md");

        var found = await Discover();

        Strings(found, "candidates", "directory")
            .ShouldBe(["ai/prompts", ".claude/commands/ds"], ignoreOrder: true);
        fixture.Directories.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task ARepositoryWithNothing_Should_StillSayWhereItLooked()
    {
        var found = await Discover();

        found.GetProperty("candidates").GetArrayLength().ShouldBe(0);
        // "we looked in these places" is an answer; a bare empty list reads as a broken button.
        Strings(found, "searchedIn").ShouldBe(["ai/prompts", ".claude/commands"]);
    }

    [Fact]
    public async Task AConfirmedDirectory_Should_WireTheRepositorysOwnFiles()
    {
        ArrangeDsConnect();

        var report = await SetUp(directory: ".claude/commands/ds");

        report.GetProperty("directory").GetString().ShouldBe(".claude/commands/ds");
        fixture.Directories.Saved.ShouldBe([".claude/commands/ds"]);

        // The repository's own file, not a copy of ours: the Automation names grill.md, and the
        // catalogue's aio-grill.md was never written anywhere.
        var automations = await Automations();
        Automation(automations, "ai:grill")
            .GetProperty("promptPath")
            .GetString()
            .ShouldBe("grill.md");
        Automation(automations, "ai:implement")
            .GetProperty("promptPath")
            .GetString()
            .ShouldBe("implement.md");

        // A file matching no step is reported, never interpreted — no Automation exists for it.
        Strings(report, "foundNotWired").ShouldContain("sprint-notes.md");
        automations
            .EnumerateArray()
            .Select(entry => entry.GetProperty("promptPath").GetString())
            .ShouldNotContain("sprint-notes.md");
    }

    [Fact]
    public async Task AnAdoptedStep_Should_NotAlsoBeInstalled()
    {
        // Every installable step already has a file: there is no gap, so there is no pull request.
        fixture.Documents.Directories["ai/prompts"] = StubDirectory.Of(
            "triage.md",
            "explain.md",
            "implement.md",
            "tests.md",
            "review.md"
        );

        var report = await SetUp(directory: "ai/prompts", installMissing: true);

        Strings(report.GetProperty("installed"), "files").ShouldBeEmpty();
        report
            .GetProperty("installed")
            .GetProperty("pullRequestUrl")
            .ValueKind.ShouldBe(JsonValueKind.Null);
        fixture.Workspace.PreparedBranch.ShouldBeNull();
    }

    [Fact]
    public async Task TheGaps_Should_ArriveAsOnePullRequest()
    {
        // One file present; every other installable step is a gap. Four gaps, one review.
        fixture.Documents.Directories["ai/prompts"] = StubDirectory.Of("triage.md");

        var report = await SetUp(directory: "ai/prompts", installMissing: true);

        var installed = report.GetProperty("installed");
        installed
            .GetProperty("pullRequestUrl")
            .GetString()
            .ShouldBe("https://github.com/acme/portal/pull/7");
        Strings(installed, "files").Count.ShouldBe(4);
        Strings(installed, "files").ShouldNotContain("ai/prompts/triage.md");

        // One branch, one draft pull request, every gap in it (design D4).
        fixture.Workspace.PreparedBranch.ShouldBe("starter/pipeline");
        fixture.Workspace.PublishedAsDraft.ShouldBe(true);
        fixture.Workspace.PublishedFiles.Count.ShouldBe(4);
        fixture.Workspace.PublishedFiles.ShouldAllBe(path => path.StartsWith("ai/prompts/"));
    }

    [Fact]
    public async Task AnOptInStep_Should_BeAdoptedButNeverInstalled()
    {
        ArrangeDsConnect();

        var report = await SetUp(directory: ".claude/commands/ds", installMissing: true);

        // grill is a step of a tier that declares a prerequisite: its file is here, so it is
        // wired — reading what a team wrote is not the act writing one would be.
        Strings(report, "created").ShouldContain("ai:grill");

        // …and nothing from that tier is in the pull request. Only steps a tier offers
        // unconditionally may be written into somebody's repository by a button.
        var installedFiles = Strings(report.GetProperty("installed"), "files");
        installedFiles.ShouldNotBeEmpty();
        installedFiles.ShouldAllBe(path => path!.EndsWith(".md") && !path.Contains("grill"));
        fixture.Workspace.PublishedFiles.ShouldAllBe(path => !path.Contains("aio-"));
    }

    [Fact]
    public async Task AnExistingTrigger_Should_BeSkippedAndSayWhy()
    {
        ArrangeDsConnect();

        var existing = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "AI:GRILL",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "mine.md",
                requiresApproval = false,
            }
        );
        existing.EnsureSuccessStatusCode();

        var report = await SetUp(directory: ".claude/commands/ds");

        var skipped = report
            .GetProperty("skipped")
            .EnumerateArray()
            .Single(entry =>
                string.Equals(
                    entry.GetProperty("trigger").GetString(),
                    "ai:grill",
                    StringComparison.OrdinalIgnoreCase
                )
            );
        skipped.GetProperty("reason").GetString().ShouldNotBeNullOrWhiteSpace();

        // Convergence never edits what exists — the Admin's own Automation is untouched.
        Automation(await Automations(), "ai:grill")
            .GetProperty("promptPath")
            .GetString()
            .ShouldBe("mine.md");
    }

    [Fact]
    public async Task AProjectWithNoConnector_Should_BeToldRatherThanSearched()
    {
        fixture.Connector.Snapshot = null;

        var found = await Discover();

        found.GetProperty("candidates").GetArrayLength().ShouldBe(0);
        found.GetProperty("reason").GetString().ShouldNotBeNull().ShouldContain("Connector");
    }

    [Fact]
    public async Task Discovery_Should_SayWhatTheButtonWouldCreate()
    {
        // #233 — the plan, before the press. It used to exist only as a report afterwards, which is
        // the wrong side of an action that writes to somebody's repository.
        ArrangeDsConnect();

        var candidate = (await Discover())
            .GetProperty("candidates")
            .EnumerateArray()
            .Single(entry => entry.GetProperty("directory").GetString() == ".claude/commands/ds");

        var plan = candidate.GetProperty("plan").EnumerateArray().ToList();
        plan.ShouldNotBeEmpty();

        // Every step the repository already has a file for is wired to *that* file and marked as
        // present — not as something to install.
        var adopted = plan.Single(step => step.GetProperty("trigger").GetString() == "ai:grill");
        adopted.GetProperty("exists").GetBoolean().ShouldBeTrue();
        adopted.GetProperty("promptFile").GetString().ShouldBe("grill.md");

        // And a step with no file in this directory says a starter would be written for it, which
        // is the distinction the checkbox used to stand in for.
        plan.ShouldContain(step => !step.GetProperty("exists").GetBoolean());
        plan.Where(step => !step.GetProperty("exists").GetBoolean())
            .ShouldAllBe(step => step.GetProperty("installable").GetBoolean());

        // Reading the plan writes nothing: discovery proposes and never picks (design D1).
        fixture.Directories.Saved.ShouldBeEmpty();
        (await Automations()).GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task ThePlan_Should_NameTheStepThatWaitsForAPerson()
    {
        // The gate is a property of the step, and the plan is where somebody decides whether they
        // want it. Naming it only in the report means learning it after it exists.
        ArrangeDsConnect();

        var candidate = (await Discover())
            .GetProperty("candidates")
            .EnumerateArray()
            .Single(entry => entry.GetProperty("directory").GetString() == ".claude/commands/ds");

        var plan = candidate.GetProperty("plan").EnumerateArray().ToList();

        plan.ShouldContain(step => step.GetProperty("gated").GetBoolean());
    }

    async Task<JsonElement> Discover()
    {
        var response = await _client.GetStringAsync(
            $"/api/projects/{_projectId}/automations/discover-pipeline"
        );
        return JsonDocument.Parse(response).RootElement.Clone();
    }

    async Task<JsonElement> SetUp(string? directory = null, bool installMissing = false)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations/set-up-defaults",
            new { promptDirectory = directory, installMissing }
        );
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    async Task<JsonElement> Automations()
    {
        var response = await _client.GetStringAsync($"/api/projects/{_projectId}/automations");
        return JsonDocument.Parse(response).RootElement.Clone();
    }

    static JsonElement Automation(JsonElement automations, string trigger) =>
        automations
            .EnumerateArray()
            .Single(entry =>
                string.Equals(
                    entry.GetProperty("triggerLabel").GetString(),
                    trigger,
                    StringComparison.OrdinalIgnoreCase
                )
            );

    static List<string?> Strings(JsonElement element, string property) =>
        [.. element.GetProperty(property).EnumerateArray().Select(entry => entry.GetString())];

    static List<string?> Strings(JsonElement element, string array, string property) =>
        [
            .. element
                .GetProperty(array)
                .EnumerateArray()
                .Select(entry => entry.GetProperty(property).GetString()),
        ];

    sealed record ProjectRef(Guid Id, string Name);
}
