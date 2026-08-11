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
        // One file present; every other step of the consented tier is a gap. Five gaps, one review.
        fixture.Documents.Directories["ai/prompts"] = StubDirectory.Of("grill.md");

        var report = await SetUp(
            directory: "ai/prompts",
            installMissing: true,
            tiers: ["workflow"]
        );

        var installed = report.GetProperty("installed");
        installed
            .GetProperty("pullRequestUrl")
            .GetString()
            .ShouldBe("https://github.com/acme/portal/pull/7");
        Strings(installed, "files").Count.ShouldBe(5);
        Strings(installed, "files").ShouldNotContain("ai/prompts/grill.md");

        // One branch, one draft pull request, every gap in it (design D4).
        fixture.Workspace.PreparedBranch.ShouldBe("starter/pipeline");
        fixture.Workspace.PublishedAsDraft.ShouldBe(true);

        // The prompts land under the chosen directory; the tier's documents deliberately do not, and
        // they travel in the same pull request (#269).
        Strings(installed, "files").ShouldAllBe(path => path!.StartsWith("ai/prompts/"));
        Strings(installed, "prerequisites").ShouldAllBe(path => !path!.StartsWith("ai/prompts/"));
    }

    [Fact]
    public async Task AnOptInStep_Should_BeAdoptedWithoutConsentAndInstalledOnlyWithIt()
    {
        ArrangeDsConnect();

        // No consent: the four files that are here are wired, because reading what a team wrote was
        // never the act in question — and nothing at all is written.
        var adopted = await SetUp(directory: ".claude/commands/ds", installMissing: true);

        Strings(adopted, "created").ShouldContain("ai:grill");
        Strings(adopted.GetProperty("installed"), "files").ShouldBeEmpty();
        Strings(adopted.GetProperty("installed"), "prerequisites").ShouldBeEmpty();
        fixture.Workspace.PreparedBranch.ShouldBeNull();
    }

    [Fact]
    public async Task AnOptInStepWithNoFile_Should_BeInstalledOnceItsTierIsConsentedTo()
    {
        ArrangeDsConnect();

        // `ds` holds grill, propose, implement and sync — so refine and status are the gaps, and only
        // a consent may fill them.
        var report = await SetUp(
            directory: ".claude/commands/ds",
            installMissing: true,
            tiers: ["workflow"]
        );

        var files = Strings(report.GetProperty("installed"), "files");
        files.ShouldBe(
            [".claude/commands/ds/aio-refine.md", ".claude/commands/ds/aio-status.md"],
            ignoreOrder: true
        );

        // The repository's own four files are wired, not rewritten under the starter's saved name.
        Automation(await Automations(), "ai:grill")
            .GetProperty("promptPath")
            .GetString()
            .ShouldBe("grill.md");
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

        // And a step with no file in this directory is offered as a row that a consent would fill.
        // `installable` stays false because the catalogue's only tier declares a prerequisite (#269):
        // it means "a starter can be written without asking", and here asking is the whole point. The
        // row still arrives, with its tier, so the card can reveal it the moment the switch goes on.
        plan.ShouldContain(step => !step.GetProperty("exists").GetBoolean());
        plan.Where(step => !step.GetProperty("exists").GetBoolean())
            .ShouldAllBe(step =>
                !step.GetProperty("installable").GetBoolean()
                && step.GetProperty("tierId").GetString() == "workflow"
            );

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

    // #262 — the Admin chooses which of the proposed steps are actually created. The property the
    // suite below pins is that **absent and empty are different answers**: one means every step,
    // the other means none, and they differ by a pull request landing in somebody's repository.

    /// <summary>
    /// A repository holding one step of the loop and none of the rest: `grill.md` is adopted, and the
    /// other five are gaps that only a consent can fill.
    /// </summary>
    const string Grill = "grill.md";

    static readonly string[] EveryStep =
    [
        "ai:grill",
        "ai:propose",
        "ai:implement",
        "ai:sync",
        "ai:refine",
        "ai:status",
    ];

    [Fact]
    public async Task AnAbsentSelection_Should_CreateEveryStep()
    {
        fixture.Documents.Directories["ai/prompts"] = StubDirectory.Of(Grill);

        var report = await SetUp(
            directory: "ai/prompts",
            installMissing: true,
            steps: null,
            tiers: ["workflow"]
        );

        // The whole set: the adopted file plus every gap the consent unlocked. An absent selection
        // still means every step — #262's contract, unchanged by #269.
        Strings(report, "created").ShouldBe(EveryStep, ignoreOrder: true);
        Strings(report, "excluded").ShouldBeEmpty();
    }

    [Fact]
    public async Task AnAbsentConsent_Should_WireOnlyWhatIsAlreadyThere()
    {
        fixture.Documents.Directories["ai/prompts"] = StubDirectory.Of(Grill);

        // No tier named: the opposite default from `steps`. The file that exists is still wired,
        // because reading a repository needs no permission — but nothing is written.
        var report = await SetUp(directory: "ai/prompts", installMissing: true, steps: null);

        Strings(report, "created").ShouldBe(["ai:grill"]);
        fixture.Workspace.PreparedBranch.ShouldBeNull();

        var installed = report.GetProperty("installed");
        Strings(installed, "files").ShouldBeEmpty();
        Strings(installed, "prerequisites").ShouldBeEmpty();
        installed.GetProperty("pullRequestUrl").ValueKind.ShouldBe(JsonValueKind.Null);
        installed.GetProperty("failure").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task AConsentedTier_Should_BringItsPromptsAndItsDocumentsInOnePullRequest()
    {
        fixture.Documents.Directories["ai/prompts"] = StubDirectory.Of(Grill);

        var report = await SetUp(
            directory: "ai/prompts",
            installMissing: true,
            tiers: ["workflow"]
        );

        var installed = report.GetProperty("installed");

        // The five gaps, under the chosen directory.
        Strings(installed, "files").Count.ShouldBe(5);
        Strings(installed, "files").ShouldContain("ai/prompts/aio-implement.md");

        // And the documents those prompts read, outside the prompt directory — reported apart from
        // the prompts, because one count standing for both would hide the writes that leave it.
        var prerequisites = Strings(installed, "prerequisites");
        prerequisites.ShouldContain("docs/process/definition-of-ready.md");
        prerequisites.ShouldContain("openspec/config.yaml");

        // One branch, one pull request, carrying both kinds.
        installed.GetProperty("branch").GetString().ShouldBe("starter/pipeline");
        installed.GetProperty("pullRequestUrl").ValueKind.ShouldNotBe(JsonValueKind.Null);
        fixture.Workspace.PublishedFiles.Count.ShouldBe(5 + prerequisites.Count);
    }

    [Fact]
    public async Task AnExistingPrerequisite_Should_BeLeftAloneAndReported()
    {
        fixture.Documents.Directories["ai/prompts"] = StubDirectory.Of(Grill);

        // The rule the seeding decision rests on (ADR-0012): a team that already has its own
        // readiness document keeps it, so the product's copy is never the weaker of two.
        fixture.Workspace.ExistingFiles.Add("docs/process/definition-of-ready.md");

        var report = await SetUp(
            directory: "ai/prompts",
            installMissing: true,
            tiers: ["workflow"]
        );

        var installed = report.GetProperty("installed");
        Strings(installed, "prerequisites").ShouldNotContain("docs/process/definition-of-ready.md");
        Strings(installed, "prerequisitesAlreadyPresent")
            .ShouldContain("docs/process/definition-of-ready.md");

        // Asserted on content, not on absence: a file the repository already had is present in the
        // clone either way, so "left alone" means its bytes did not change.
        fixture
            .Workspace.PublishedContents["docs/process/definition-of-ready.md"]
            .ShouldBe(StubInstallWorkspace.TheProjectsOwnContent);

        // The prompts still install: one document being present says nothing about the rest.
        Strings(installed, "files").Count.ShouldBe(5);
    }

    [Fact]
    public async Task AConsentedTierWithNoGap_Should_StillBringItsDocuments()
    {
        // Every prompt already there, so there is no gap at all — and the documents may still be
        // missing. This is the case the old empty-gap short-circuit swallowed.
        fixture.Documents.Directories["ai/prompts"] = StubDirectory.Of(
            "grill.md",
            "propose.md",
            "implement.md",
            "sync.md",
            "refine.md",
            "status.md"
        );

        var report = await SetUp(
            directory: "ai/prompts",
            installMissing: true,
            tiers: ["workflow"]
        );

        var installed = report.GetProperty("installed");
        Strings(installed, "files").ShouldBeEmpty();
        Strings(installed, "prerequisites").ShouldNotBeEmpty();
        installed.GetProperty("pullRequestUrl").ValueKind.ShouldNotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task EverythingAlreadyPresent_Should_WriteNothingAndNotFail()
    {
        fixture.Documents.Directories["ai/prompts"] = StubDirectory.Of(
            "grill.md",
            "propose.md",
            "implement.md",
            "sync.md",
            "refine.md",
            "status.md"
        );
        foreach (var path in PrerequisitePaths)
        {
            fixture.Workspace.ExistingFiles.Add(path);
        }

        var report = await SetUp(
            directory: "ai/prompts",
            installMissing: true,
            tiers: ["workflow"]
        );

        var installed = report.GetProperty("installed");
        Strings(installed, "files").ShouldBeEmpty();
        Strings(installed, "prerequisites").ShouldBeEmpty();
        Strings(installed, "prerequisitesAlreadyPresent").ShouldNotBeEmpty();

        // Nothing to write is a clean outcome, never a refusal — no pull request and no failure.
        installed.GetProperty("pullRequestUrl").ValueKind.ShouldBe(JsonValueKind.Null);
        installed.GetProperty("failure").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task AnEmptySelection_Should_CreateNothingAndOpenNoPullRequest()
    {
        fixture.Documents.Directories["ai/prompts"] = StubDirectory.Of(Grill);

        // Consented **and** every step excluded. Consent answers "may this be installed"; the
        // selection answers "what is being created". Nothing is being created, so the tier is not
        // being installed and its documents must not arrive either.
        var report = await SetUp(
            directory: "ai/prompts",
            installMissing: true,
            steps: [],
            tiers: ["workflow"]
        );

        Strings(report, "created").ShouldBeEmpty();
        (await Automations()).GetArrayLength().ShouldBe(0);
        Strings(report, "excluded").ShouldBe(EveryStep, ignoreOrder: true);

        // Nothing to write is not a refusal: no branch, no pull request, and no failure reported
        // for a decision the Admin made.
        fixture.Workspace.PreparedBranch.ShouldBeNull();
        var installed = report.GetProperty("installed");
        Strings(installed, "files").ShouldBeEmpty();
        Strings(installed, "prerequisites").ShouldBeEmpty();
        installed.GetProperty("pullRequestUrl").ValueKind.ShouldBe(JsonValueKind.Null);
        installed.GetProperty("failure").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task APartialSelection_Should_CreateOnlyTheSelectedSteps()
    {
        fixture.Documents.Directories["ai/prompts"] = StubDirectory.Of(Grill);

        var report = await SetUp(
            directory: "ai/prompts",
            installMissing: true,
            steps: ["ai:grill", "ai:propose"],
            tiers: ["workflow"]
        );

        Strings(report, "created").ShouldBe(["ai:grill", "ai:propose"], ignoreOrder: true);
        Strings(report, "excluded")
            .ShouldBe(["ai:implement", "ai:sync", "ai:refine", "ai:status"], ignoreOrder: true);

        var automations = await Automations();
        automations.GetArrayLength().ShouldBe(2);
        automations
            .EnumerateArray()
            .Select(entry => entry.GetProperty("triggerLabel").GetString())
            .ShouldNotContain("ai:sync");

        // Some steps survived, so the tier is being acted on and its documents arrive once.
        Strings(report.GetProperty("installed"), "prerequisites").ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AnExcludedGap_Should_BeAbsentFromThePullRequest()
    {
        fixture.Documents.Directories["ai/prompts"] = StubDirectory.Of(Grill);

        var report = await SetUp(
            directory: "ai/prompts",
            installMissing: true,
            steps: ["ai:grill", "ai:propose", "ai:implement", "ai:refine", "ai:status"],
            tiers: ["workflow"]
        );

        // ai:sync was the one gap left out — its starter is written nowhere.
        var files = Strings(report.GetProperty("installed"), "files");
        files.Count.ShouldBe(4);
        files.ShouldNotContain("ai/prompts/aio-sync.md");
        fixture.Workspace.PublishedFiles.ShouldNotContain("ai/prompts/aio-sync.md");
    }

    [Fact]
    public async Task ExcludingEveryGap_Should_StillBringTheDocumentsOfAnAdoptedTier()
    {
        fixture.Documents.Directories["ai/prompts"] = StubDirectory.Of(Grill);

        // Only the step whose file is already here. No prompt gap survives, so no starter is
        // written — but the tier is still being acted on, so its documents are.
        var report = await SetUp(
            directory: "ai/prompts",
            installMissing: true,
            steps: ["ai:grill"],
            tiers: ["workflow"]
        );

        Strings(report, "created").ShouldBe(["ai:grill"]);

        var installed = report.GetProperty("installed");
        Strings(installed, "files").ShouldBeEmpty();
        Strings(installed, "prerequisites").ShouldNotBeEmpty();
        installed.GetProperty("failure").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task AnExcludedStep_Should_NotAlsoBeReportedAsSkipped()
    {
        fixture.Documents.Directories["ai/prompts"] = StubDirectory.Of(Grill);

        var existing = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations",
            new
            {
                triggerLabel = "ai:grill",
                triggerState = (string?)null,
                action = "RepositoryPrompt",
                runtime = "ClaudeCodeHeadless",
                promptPath = "mine.md",
                requiresApproval = false,
            }
        );
        existing.EnsureSuccessStatusCode();

        var report = await SetUp(
            directory: "ai/prompts",
            steps: ["ai:propose"],
            tiers: ["workflow"]
        );

        // Excluded and already-taken are different facts, and the filter runs first — so the step
        // the Admin left out never reaches the skip path and lands in exactly one list.
        Strings(report, "excluded").ShouldContain("ai:grill");
        Strings(report, "skipped", "trigger").ShouldNotContain("ai:grill");
    }

    [Fact]
    public async Task ASelection_Should_MatchTriggersWhateverTheirCaseAndIgnoreUnknownOnes()
    {
        fixture.Documents.Directories["ai/prompts"] = StubDirectory.Of(Grill);

        var report = await SetUp(
            directory: "ai/prompts",
            steps: ["AI:GRILL", "ai:does-not-exist"],
            tiers: ["workflow"]
        );

        // The BR-003 identity, so a selection cannot be accepted and then silently match nothing.
        Strings(report, "created").ShouldBe(["ai:grill"]);

        // A name this invocation would not have acted on invents no work and is not an error.
        (await Automations())
            .GetArrayLength()
            .ShouldBe(1);
    }

    [Fact]
    public async Task ThePlan_Should_CarryWhichTransitionEachStepClaimsAndWhichTierItIsFrom()
    {
        // The card computes both the broken-hand-off marker and which rows a consent reveals, on a
        // click — so the claim and the tier have to arrive with the plan. A round trip per checkbox
        // is not an answer.
        fixture.Documents.Directories["ai/prompts"] = StubDirectory.Of(Grill);

        var candidate = (await Discover())
            .GetProperty("candidates")
            .EnumerateArray()
            .Single(entry => entry.GetProperty("directory").GetString() == "ai/prompts");

        var plan = candidate.GetProperty("plan").EnumerateArray().ToList();

        // The catalogue's own transitions (#273, restated for #310): the spec-first tier claims
        // grill → propose → implement → sync, so the plan carries exactly those — and the marker #262
        // built is exercisable again. Asserted by value, not just by count: the claim is what the
        // card's broken-hand-off computation consumes, and it is single-valued now because branching
        // is unrepresentable (AC 13).
        string? ClaimOf(string trigger) =>
            plan.Single(step => step.GetProperty("trigger").GetString() == trigger)
                .GetProperty("toStage")
                .GetString();

        ClaimOf("ai:grill").ShouldBe("ai:propose");
        ClaimOf("ai:propose").ShouldBe("ai:implement");
        ClaimOf("ai:implement").ShouldBe("ai:sync");
        ClaimOf("ai:sync").ShouldBeNull();
        ClaimOf("ai:refine").ShouldBeNull();
        ClaimOf("ai:status").ShouldBeNull();

        // Every row names its tier, which is what lets a consent add and remove rows client-side.
        plan.ShouldAllBe(step => step.GetProperty("tierId").GetString() == "workflow");

        var grill = plan.Single(step => step.GetProperty("trigger").GetString() == "ai:grill");
        grill.GetProperty("exists").GetBoolean().ShouldBeTrue();

        // A gated tier's step is not installable until its tier is consented to.
        var sync = plan.Single(step => step.GetProperty("trigger").GetString() == "ai:sync");
        sync.GetProperty("exists").GetBoolean().ShouldBeFalse();
        sync.GetProperty("installable").GetBoolean().ShouldBeFalse();

        // Still no extra vendor read: the plan comes from the listing discovery already performed.
        fixture.Directories.Saved.ShouldBeEmpty();
    }

    async Task<JsonElement> Discover()
    {
        var response = await _client.GetStringAsync(
            $"/api/projects/{_projectId}/automations/discover-pipeline"
        );
        return JsonDocument.Parse(response).RootElement.Clone();
    }

    /// <summary>Every path the workflow tier's consent would write, in manifest order.</summary>
    static readonly string[] PrerequisitePaths =
    [
        "docs/process/definition-of-ready.md",
        "docs/process/backlog-shaping-rules.md",
        "docs/process/product-context.md",
        "docs/process/retro-log.md",
        "openspec/config.yaml",
        "openspec/specs/.gitkeep",
        "openspec/changes/archive/.gitkeep",
    ];

    async Task<JsonElement> SetUp(
        string? directory = null,
        bool installMissing = false,
        string[]? steps = null,
        string[]? tiers = null
    )
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{_projectId}/automations/set-up-defaults",
            new
            {
                promptDirectory = directory,
                installMissing,
                steps,
                tiers,
            }
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
