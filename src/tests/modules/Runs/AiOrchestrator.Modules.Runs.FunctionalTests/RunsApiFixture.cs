using System.Collections.Concurrent;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Dispatch;
using AiOrchestrator.BuildingBlocks.IntegrationEvents;
using AiOrchestrator.BuildingBlocks.Secrets;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Backlog.Domain;
using AiOrchestrator.Modules.Backlog.Persistence;
using AiOrchestrator.Modules.Runs.Persistence;
using AiOrchestrator.ServiceDefaults.Dispatch;
using AiOrchestrator.SharedFunctionalTests;
using Azure.Storage.Queues;
using ErrorOr;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Runs.FunctionalTests;

/// <summary>
/// Matching end-to-end against real containers: the vendor stubbed at the
/// <see cref="IBacklogConnector"/> seam, everything downstream real — the reconciler, the CAP
/// relay, the Runs handler, Postgres, and the Azurite-backed dispatch queue.
/// </summary>
public sealed class RunsApiFixture : ApiServiceFixtureBase
{
    internal StubBacklogConnector Vendor { get; } = new();

    /// <summary>
    /// The delivery fence (see the #41 retro): registered after the module handlers, so by the
    /// time a delivery is recorded here, the Runs handler for that delivery has completed.
    /// </summary>
    internal DeliveryProbe Probe { get; } = new();

    /// <summary>The runtime faked at the seam — the same discipline as the vendor stub.</summary>
    internal FakeAgentRuntime Agent { get; } = new();

    /// <summary>The second runtime's fake — selection tests prove each name reaches its own.</summary>
    internal FakeAgentRuntime OpenCodeAgent { get; } = new();

    /// <summary>Secret names the host resolved — the free-model path must not add any.</summary>
    internal ResolvedNames SecretNames { get; } = new();

    /// <summary>The ceremony faked at its seam: scripted preparation and publication.</summary>
    internal FakeCodeWorkspace Workspace { get; } = new();

    /// <summary>
    /// The pod host faked at the monitor seam (design review 5b): the panel's endpoint is about
    /// joining sightings to Runs, never about docker — so the tests hand it sightings directly.
    /// </summary>
    internal FakeAgentPodsMonitor Pods { get; } = new();

    /// <summary>
    /// The conversation runtime, faked at its seam (#166). Faked rather than driven through the
    /// in-process implementation, because what these tests are about is what the module does with a
    /// reply — one pass per message, usage recorded, a failure left open — and not how an agent
    /// produces one.
    /// </summary>
    internal FakeConversationRuntime Conversations { get; } = new();

    // "projects" is spelled out: ProjectsDbContext is internal to its module, and a schema
    // constant is not worth an InternalsVisibleTo.
    protected override string[] SchemasToReset =>
        [RunsDbContext.Schema, BacklogDbContext.Schema, "projects"];

    /// <summary>The same queue the product writes, through the same pinned wire version.</summary>
    public QueueClient Queue =>
        new(StorageConnectionString, DispatchQueue.Name, DispatchQueue.ClientOptions());

    public async Task ResetQueue()
    {
        await Queue.CreateIfNotExistsAsync();
        await Queue.ClearMessagesAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("Backlog:PollingEnabled", "false");
        builder.UseSetting("Runs:ResumeCheckEnabled", "false");
        // #140 — the sweep is driven directly by the tests rather than by a timer, for the same
        // reason the poller is off: a background tick firing mid-assertion is a flake generator.
        builder.UseSetting("Runs:ReapingEnabled", "false");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBacklogConnector>();
            services.AddSingleton<IBacklogConnector>(Vendor);
            services.AddSingleton<ISecretResolver>(new StubSecretResolver(SecretNames));

            services.AddSingleton(Probe);
            services.AddIntegrationEventHandler<StoryChanged, DeliveryProbe.Handler>();

            // Selection faked at ITS seam: each runtime name maps to its own recording fake;
            // the OpenCode entry carries no credential name (free model, design D3).
            services.RemoveAll<IConversationRuntime>();
            services.AddSingleton<IConversationRuntime>(Conversations);

            services.RemoveAll<IAgentRuntimeSelector>();
            services.AddSingleton<IAgentRuntimeSelector>(
                new FakeRuntimeSelector(
                    new Dictionary<string, AgentRuntimeSelection>(StringComparer.Ordinal)
                    {
                        ["ClaudeCodeHeadless"] = new(Agent, "anthropic-api-key"),
                        ["OpenCode"] = new(OpenCodeAgent, null),
                    }
                )
            );

            services.RemoveAll<ICodeWorkspace>();
            services.AddSingleton<ICodeWorkspace>(Workspace);

            services.RemoveAll<IAgentPodsMonitor>();
            services.AddSingleton<IAgentPodsMonitor>(Pods);
        });
    }
}

/// <summary>A pod host whose snapshot the test decides; unhosted until one does.</summary>
sealed class FakeAgentPodsMonitor : IAgentPodsMonitor
{
    public AgentPodsSnapshot Next { get; set; } = AgentPodsSnapshot.Unhosted;

    public void Reset() => Next = AgentPodsSnapshot.Unhosted;

    public AgentPodsSnapshot Snapshot() => Next;
}

/// <summary>A vendor whose responses the test decides.</summary>
sealed class StubBacklogConnector : IBacklogConnector
{
    public BacklogVendor Vendor => BacklogVendor.GitHub;

    public List<VendorStory> Stories { get; } = [];

    public void Reset()
    {
        Stories.Clear();
        Files.Clear();
        Comments.Clear();
        WriteStateError = null;
        Change = null;
        Open.Clear();
        OpenChangesError = null;
        StoryComments.Clear();
        ReadCommentsError = null;
        Documents.Clear();
        // Every Run resolves a prompt since #162. Seeded here so each suite is about what it is
        // named for rather than about a missing file; a suite testing that refusal clears it.
        Documents[DefaultPromptPath] = "Do what the story asks.";
        RepositoryLabels.Clear();
        EnsureLabelError = null;
        FailNextLabelWrite = null;
    }

    /// <summary>The change a Run's Story links to, when a test wants one.</summary>
    public LinkedChange? Change { get; set; }

    /// <summary>The repository's open changes, appendable by tests (inbox-open-prs).</summary>
    public List<OpenChange> Open { get; } = [];

    public Error? OpenChangesError { get; set; }

    public Task<ErrorOr<IReadOnlyList<OpenChange>>> OpenChanges(
        BacklogCoordinates coordinates,
        string token,
        CancellationToken cancellationToken
    ) =>
        Task.FromResult<ErrorOr<IReadOnlyList<OpenChange>>>(
            OpenChangesError is { } error
                ? error
                : ErrorOrFactory.From<IReadOnlyList<OpenChange>>([.. Open])
        );

    /// <summary>
    /// Labels that exist in the *repository*, as distinct from labels on a Story. The
    /// distinction is the point of the test: ensuring a label must not touch anybody's backlog
    /// item.
    /// </summary>
    public HashSet<string> RepositoryLabels { get; } = [];

    public Error? EnsureLabelError { get; set; }

    public Task<ErrorOr<Success>> EnsureLabel(
        BacklogCoordinates coordinates,
        string label,
        string token,
        CancellationToken cancellationToken
    )
    {
        if (EnsureLabelError is { } error)
        {
            return Task.FromResult<ErrorOr<Success>>(error);
        }

        RepositoryLabels.Add(label);
        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }

    /// <summary>The Story's comments as the vendor holds them, appendable by tests.</summary>
    public List<(string StoryId, StoryComment Comment)> StoryComments { get; } = [];

    public Error? ReadCommentsError { get; set; }

    public Task<ErrorOr<IReadOnlyList<StoryComment>>> ReadComments(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        DateTimeOffset since,
        string token,
        CancellationToken cancellationToken
    ) =>
        ReadCommentsError is { } error
            ? Task.FromResult<ErrorOr<IReadOnlyList<StoryComment>>>(error)
            : Task.FromResult<ErrorOr<IReadOnlyList<StoryComment>>>(
                ErrorOrFactory.From<IReadOnlyList<StoryComment>>([
                    .. StoryComments
                        .Where(entry =>
                            entry.StoryId == vendorStoryId && entry.Comment.CreatedAt >= since
                        )
                        .Select(entry => entry.Comment)
                        .OrderBy(comment => comment.CreatedAt),
                ])
            );

    // These tests never configure a Connector through the API, so the verdict is uniformly a pass
    // (#132). What they exercise is execution, and a credential question there would be noise.
    public Task<CredentialVerdict> VerifyAccess(
        BacklogCoordinates coordinates,
        IReadOnlyList<ConnectorCapability> capabilities,
        string token,
        CancellationToken cancellationToken
    ) =>
        // The Runs suite never verifies a credential; the member exists so the fake stays a
        // complete connector, and it answers for whatever set it is handed (#226).
        Task.FromResult(
            new CredentialVerdict([
                .. capabilities.Select(capability => CapabilityResult.Passed(capability.Name)),
            ])
        );

    public Task<ErrorOr<BacklogSnapshot>> FetchStories(
        BacklogCoordinates coordinates,
        string token,
        CancellationToken cancellationToken
    ) => Task.FromResult<ErrorOr<BacklogSnapshot>>(new BacklogSnapshot([.. Stories]));

    /// <summary>
    /// Set to make the next label write refuse, then it clears itself. The stub could only ever
    /// succeed before, so "the vendor said no" was a branch nothing exercised (#115).
    /// </summary>
    public string? FailNextLabelWrite { get; set; }

    public Task<ErrorOr<Success>> ApplyLabel(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string label,
        string token,
        CancellationToken cancellationToken
    )
    {
        if (FailNextLabelWrite is { } refusal)
        {
            FailNextLabelWrite = null;
            return Task.FromResult<ErrorOr<Success>>(Error.Failure(description: refusal));
        }

        var index = Stories.FindIndex(story => story.VendorId == vendorStoryId);
        if (index >= 0 && !Stories[index].Labels.Contains(label))
        {
            Stories[index] = Stories[index] with { Labels = [.. Stories[index].Labels, label] };
        }

        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }

    public Task<ErrorOr<Success>> RemoveLabel(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string label,
        string token,
        CancellationToken cancellationToken
    )
    {
        var index = Stories.FindIndex(story => story.VendorId == vendorStoryId);
        if (index >= 0)
        {
            Stories[index] = Stories[index] with
            {
                Labels = [.. Stories[index].Labels.Where(existing => existing != label)],
            };
        }

        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }

    // The Runs tier never reads documents; the seam still has to be satisfied honestly.

    /// <summary>Comments the test can read back — the observable side of UC-017.</summary>
    public List<string> Comments { get; } = [];

    public Error? WriteStateError { get; set; }

    public Task<ErrorOr<VendorStory?>> FetchStory(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string token,
        CancellationToken cancellationToken
    ) =>
        Task.FromResult<ErrorOr<VendorStory?>>(
            Stories.FirstOrDefault(story => story.VendorId == vendorStoryId)
        );

    public Task<ErrorOr<Success>> AddComment(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string comment,
        string token,
        CancellationToken cancellationToken
    )
    {
        Comments.Add(comment);
        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }

    public Task<ErrorOr<Success>> SetState(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string state,
        string token,
        CancellationToken cancellationToken
    )
    {
        if (WriteStateError is { } error)
        {
            return Task.FromResult<ErrorOr<Success>>(error);
        }

        var index = Stories.FindIndex(story => story.VendorId == vendorStoryId);
        if (index >= 0)
        {
            Stories[index] = Stories[index] with { State = state };
        }

        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }

    public Task<ErrorOr<LinkedChange?>> FindLinkedChange(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string token,
        CancellationToken cancellationToken
    ) => Task.FromResult<ErrorOr<LinkedChange?>>(Change);

    /// <summary>The change files a Run's detail page would show.</summary>
    public List<ChangedFile> Files { get; } = [];

    public Task<ErrorOr<IReadOnlyList<ChangedFile>>> ListChangeFiles(
        BacklogCoordinates coordinates,
        int changeNumber,
        string token,
        CancellationToken cancellationToken
    ) => Task.FromResult<ErrorOr<IReadOnlyList<ChangedFile>>>(Files);

    /// <summary>The prompt every test Automation names (#162 made naming one required).</summary>
    public const string DefaultPromptName = "story.md";

    internal const string DefaultPromptPath = "ai/prompts/story.md";

    /// <summary>Repository documents by path; a path not present reads as the vendor's 404.</summary>
    public Dictionary<string, string> Documents { get; } = [];

    public Task<ErrorOr<string>> ReadDocument(
        BacklogCoordinates coordinates,
        string path,
        string reference,
        string token,
        CancellationToken cancellationToken
    ) =>
        Task.FromResult<ErrorOr<string>>(
            Documents.TryGetValue(path, out var content)
                ? content
                : AiOrchestrator.Modules.Backlog.Domain.BacklogErrors.DocumentNotFound(path)
        );

    public Task<ErrorOr<DirectoryEntries?>> ListDirectoryFiles(
        BacklogCoordinates coordinates,
        string path,
        string token,
        CancellationToken cancellationToken
    )
    {
        // The Runs tests never list prompts; the seam member exists so the fake stays a
        // complete connector.
        DirectoryEntries? none = null;
        return Task.FromResult(ErrorOr.ErrorOrFactory.From(none));
    }
}

sealed class StubSecretResolver(ResolvedNames names) : ISecretResolver
{
    public Task<string> Resolve(string secretName, CancellationToken cancellationToken = default)
    {
        names.Record(secretName);
        return Task.FromResult("stub-token");
    }
}

/// <summary>Every name the host asked the vault for — BR-010's observable half.</summary>
sealed class ResolvedNames
{
    readonly ConcurrentQueue<string> _names = new();

    public IReadOnlyList<string> All => [.. _names];

    public void Clear() => _names.Clear();

    public void Record(string name) => _names.Enqueue(name);
}

sealed class FakeRuntimeSelector(IReadOnlyDictionary<string, AgentRuntimeSelection> runtimes)
    : IAgentRuntimeSelector
{
    public AgentRuntimeSelection? For(string runtimeName) =>
        runtimes.TryGetValue(runtimeName, out var selection) ? selection : null;
}

/// <summary>
/// Records completed deliveries per Project. Because it registers after the module handlers,
/// a recorded delivery means the Runs handler for it has already returned — the fence that
/// makes every "nothing happened" assertion deterministic.
/// </summary>
sealed class DeliveryProbe
{
    readonly ConcurrentQueue<StoryChanged> _deliveries = new();

    public IReadOnlyList<StoryChanged> For(Guid projectId) =>
        [.. _deliveries.Where(delivery => delivery.ProjectId == projectId)];

    public async Task<IReadOnlyList<StoryChanged>> WaitForAtLeast(
        Guid projectId,
        int count,
        TimeSpan? timeout = null
    )
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));

        while (DateTime.UtcNow < deadline && For(projectId).Count < count)
        {
            await Task.Delay(100);
        }

        return For(projectId);
    }

    internal sealed class Handler(DeliveryProbe probe) : IIntegrationEventHandler<StoryChanged>
    {
        public Task Handle(StoryChanged @event, CancellationToken cancellationToken)
        {
            probe._deliveries.Enqueue(@event);
            return Task.CompletedTask;
        }
    }
}

/// <summary>
/// A runtime whose result the test scripts. Instructions are recorded so tests can assert what
/// crossed the seam — values in memory, never names.
/// </summary>
sealed class FakeAgentRuntime : IAgentRuntime
{
    readonly ConcurrentQueue<AgentInstruction> _instructions = new();

    public AgentResult Result { get; set; } =
        new(Succeeded: true, Log: "ok", OutputLink: null, Usage: new AgentUsage(10, 20, 0.05m));

    public Exception? Throws { get; set; }

    /// <summary>
    /// Runs inside the invocation — the only honest way to make "cancelled mid-flight"
    /// deterministic rather than a sleep-and-hope race.
    /// </summary>
    public Func<Task>? OnExecute { get; set; }

    public IReadOnlyList<AgentInstruction> Instructions => [.. _instructions];

    public void Reset()
    {
        _instructions.Clear();
        Throws = null;
        OnExecute = null;
        Result = new AgentResult(
            Succeeded: true,
            Log: "ok",
            OutputLink: null,
            Usage: new AgentUsage(10, 20, 0.05m)
        );
    }

    public async Task<AgentResult> Execute(
        AgentInstruction instruction,
        CancellationToken cancellationToken
    )
    {
        _instructions.Enqueue(instruction);

        // Forward the scripted transcript line by line, as the real wrappers do (#96) — the
        // tests exercise the actual writer, not a parallel path.
        if (instruction.OnOutput is { } sink)
        {
            foreach (var line in Result.Log.Split('\n'))
            {
                sink(line);
            }
        }

        if (OnExecute is { } during)
        {
            await during();
        }

        return Throws is { } exception ? throw exception : Result;
    }
}

/// <summary>
/// A workspace whose stages the test scripts: refuse preparation, refuse publication, or
/// publish to a scripted PR URL. Real directories are created so the executor's cleanup and
/// the runtime's working directory stay honest.
/// </summary>
sealed class FakeCodeWorkspace : ICodeWorkspace
{
    public Error? PrepareError { get; set; }

    public Error? PublishError { get; set; }

    public string PullRequestUrl { get; set; } = "https://github.com/acme/portal/pull/1";

    /// <summary>Whether Publish was reached — phase 1 must never get here (approval-gate).</summary>
    public bool Published { get; private set; }

    /// <summary>Whether a workspace was prepared at all — propose's refusals must precede it.</summary>
    public bool Prepared { get; private set; }

    public void Reset()
    {
        PrepareError = null;
        PublishError = null;
        Published = false;
        Prepared = false;
        PullRequestUrl = "https://github.com/acme/portal/pull/1";
    }

    public Task<ErrorOr<PreparedWorkspace>> Prepare(
        CodeCoordinates coordinates,
        Guid runId,
        string token,
        CancellationToken cancellationToken
    ) => Prepare(coordinates, $"run/{runId}", token, cancellationToken);

    public Task<ErrorOr<PreparedWorkspace>> Prepare(
        CodeCoordinates coordinates,
        string branch,
        string token,
        CancellationToken cancellationToken
    ) =>
        Task.FromResult<ErrorOr<PreparedWorkspace>>(
            PrepareError is { } error
                ? error
                : Track(
                    new PreparedWorkspace(
                        coordinates,
                        Directory.CreateTempSubdirectory("fake-ws-").FullName,
                        branch
                    )
                )
        );

    PreparedWorkspace Track(PreparedWorkspace workspace)
    {
        Prepared = true;
        return workspace;
    }

    public Task<ErrorOr<PublishedChange>> Publish(
        PreparedWorkspace workspace,
        string title,
        string body,
        string token,
        CancellationToken cancellationToken,
        bool draft = false
    )
    {
        Published = true;
        return Task.FromResult<ErrorOr<PublishedChange>>(
            PublishError is { } error ? error : new PublishedChange(PullRequestUrl)
        );
    }
}

[CollectionDefinition(Name)]
public sealed class RunsCollection : ICollectionFixture<RunsApiFixture>
{
    public const string Name = "Runs";
}

/// <summary>
/// A scripted conversation runtime. Counts its passes, because "exactly one agent pass per message"
/// is ADR-0008's whole model and a test that did not count could not tell one from two.
/// </summary>
sealed class FakeConversationRuntime : IConversationRuntime
{
    int _passes;

    public int Passes => _passes;

    /// <summary>What the next pass returns. Default: a success with usage.</summary>
    public ConversationReply Next { get; set; } =
        new(true, "Because the connector's token expired.", new AgentUsage(120, 80, 0.004m));

    /// <summary>
    /// Every pass, whole. The context alone was enough while a message was only a question; a
    /// scratchpad attempt is the prompt itself (#189), so what was said and which conversation said
    /// it are both assertable now — the second is how "each attempt is tried afresh" is checked.
    /// </summary>
    public List<(Guid ConversationId, ConversationContext Context, string Message)> Calls { get; } =
    [];

    /// <summary>Every context the module handed over — what the assertions about grounding read.</summary>
    public List<ConversationContext> Contexts => [.. Calls.Select(call => call.Context)];

    public Task<ConversationReply> Answer(
        Guid conversationId,
        ConversationContext context,
        string message,
        CancellationToken cancellationToken = default
    )
    {
        Interlocked.Increment(ref _passes);
        Calls.Add((conversationId, context, message));
        return Task.FromResult(Next);
    }

    public void Reset()
    {
        _passes = 0;
        Calls.Clear();
        Next = new(true, "Because the connector's token expired.", new AgentUsage(120, 80, 0.004m));
    }
}
