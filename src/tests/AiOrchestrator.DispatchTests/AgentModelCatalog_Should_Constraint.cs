using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.ServiceDefaults.Agents;
using Shouldly;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// Where a model chooser's options come from (#291). Two mechanisms, because two CLIs: opencode
/// answers `opencode models`, Claude Code has no such command and reads an operator's list. The
/// fakes here are written so each assertion CAN fail — the host records what it was asked, so a
/// catalog that quietly asked the wrong runtime, or asked one it should not have, is caught rather
/// than passing on a matching count.
/// </summary>
public class AgentModelCatalog_Should_Constraint
{
    [Fact]
    public async Task ARuntimeThatCanBeAsked_Should_BeAsked()
    {
        // Measured 2026-08-08: `opencode models` answers, so a copied list would drift the moment
        // a provider ships anything — and this one is 495 entries deep inside a sandbox.
        var host = new RecordingHost(["opencode/one", "opencode/two"]);
        var catalog = Catalog(host, Selection("opencode", listWith: ["models"]));

        var options = await catalog.For("OpenCode");

        options.Source.ShouldBe(AgentModelSource.Enumerated);
        options.Models.ShouldBe(["opencode/one", "opencode/two"]);
        host.Asked.ShouldBe([("opencode", "models")]);
    }

    [Fact]
    public async Task ARuntimeThatCannotBeAsked_Should_ReadTheOperatorsList()
    {
        // `claude --help` documents --model and no listing command exists. A list in code would
        // ship broken: `opus` resolves to a model this seat lacks and `fable` is not an alias.
        var host = new RecordingHost(["should-never-be-read"]);
        var catalog = Catalog(
            host,
            Selection("claude", listWith: null, declared: ["sonnet", "opus"])
        );

        var options = await catalog.For("ClaudeCodeHeadless");

        options.Source.ShouldBe(AgentModelSource.Declared);
        options.Models.ShouldBe(["sonnet", "opus"]);
        // The load-bearing half: a runtime that cannot enumerate must not be enumerated anyway.
        host.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task AMachineThatCouldNotBeAsked_Should_NotLookLikeARuntimeWithNoModels()
    {
        // The distinction design D6 exists for. Collapsing these tells a developer their runtime
        // has no models when in fact nobody managed to look.
        var catalog = Catalog(
            new RecordingHost(answer: null),
            Selection("opencode", listWith: ["models"], declared: ["a-declared-fallback"])
        );

        var options = await catalog.For("OpenCode");

        options.Source.ShouldBe(AgentModelSource.CouldNotAsk);
        options.Models.ShouldBeEmpty();
        // And it must NOT quietly answer a different question with the configured list — that
        // would look like success and hide an unreachable machine.
        options.Models.ShouldNotContain("a-declared-fallback");
    }

    [Fact]
    public async Task ARuntimeNobodyRegistered_Should_DeclareNothingRatherThanFailToAsk()
    {
        var catalog = Catalog(new RecordingHost([]), selection: null);

        var options = await catalog.For("NotARuntime");

        // There is no machine that failed: reporting one would invent a fault.
        options.Source.ShouldBe(AgentModelSource.Declared);
        options.Models.ShouldBeEmpty();
    }

    [Fact]
    public void TheParser_Should_KeepNamesAndDropTheCliTalking()
    {
        // `opencode models` prints one name per line. Everything a terminal adds around that is
        // noise, and a "model" with a space in it is the CLI talking rather than listing.
        var parsed = AgentModelListing.Parse(
            "opencode/deepseek-v4-flash-free\n\n  github-copilot/claude-opus-4.6  \n"
                + "0 credentials found\nopencode/deepseek-v4-flash-free\n"
        );

        parsed.ShouldBe(["opencode/deepseek-v4-flash-free", "github-copilot/claude-opus-4.6"]);
    }

    static AgentModelCatalog Catalog(IAgentProcessHost host, AgentRuntimeSelection? selection) =>
        new(new OneRuntime(selection), host);

    static AgentRuntimeSelection Selection(
        string command,
        IReadOnlyList<string>? listWith,
        IReadOnlyList<string>? declared = null
    ) =>
        new(new NeverRunsRuntime(), CredentialSecretName: null, command, InstallCommand: "install")
        {
            ModelListArguments = listWith,
            ConfiguredModels = declared ?? [],
        };

    // ---- Stand-ins ----

    /// <summary>
    /// Records what it was asked, so "did not ask" is an assertion rather than an assumption.
    /// A null answer is the host's way of saying it could not ask at all.
    /// </summary>
    sealed class RecordingHost(IReadOnlyList<string>? answer) : IAgentProcessHost
    {
        public List<(string Command, string Arguments)> Asked { get; } = [];

        public Task<IReadOnlyList<string>?> ListModels(
            string command,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken
        )
        {
            Asked.Add((command, string.Join(' ', arguments)));
            return Task.FromResult(answer);
        }

        public bool SuppliesCredentials => false;
        public string CredentialSource => "test";

        public Task<AgentProcessOutcome> Run(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            IReadOnlyDictionary<string, string> environment,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            Action<string>? onOutput = null,
            RunPreview? preview = null,
            Guid? projectId = null,
            Guid? runId = null
        ) => throw new NotSupportedException("This test never runs an agent.");

        public Task<AgentHostReadiness> CheckReadiness(CancellationToken cancellationToken) =>
            Task.FromResult(AgentHostReadiness.Local);

        public Task<bool> CliAnswers(string command, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public SessionCarriageGap? SessionUnavailableFor(
            string runtimeName,
            string command,
            string? credentialSecretName
        ) => null;
    }

    sealed class OneRuntime(AgentRuntimeSelection? selection) : IAgentRuntimeSelector
    {
        public AgentRuntimeSelection? For(string runtimeName) => selection;

        public IReadOnlyDictionary<string, AgentRuntimeSelection> Registered =>
            selection is null
                ? new Dictionary<string, AgentRuntimeSelection>()
                : new Dictionary<string, AgentRuntimeSelection> { ["only"] = selection };
    }

    sealed class NeverRunsRuntime : IAgentRuntime
    {
        public Task<AgentResult> Execute(
            AgentInstruction instruction,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException("This test never runs an agent.");
    }
}
