using System.Collections.Concurrent;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.IntegrationEvents;
using AiOrchestrator.BuildingBlocks.Secrets;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Backlog.Domain;
using AiOrchestrator.Modules.Backlog.Persistence;
using AiOrchestrator.SharedFunctionalTests;
using ErrorOr;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Backlog.FunctionalTests;

/// <summary>
/// The Backlog module against real containers, with the <b>vendor</b> stubbed at the
/// <see cref="IBacklogConnector"/> seam.
/// <para>
/// Stubbing at the seam rather than at HTTP is deliberate: the tier stays hermetic — no network,
/// no GitHub token, no rate limit — while still exercising the real handlers, the real
/// reconciliation, and the real database. The GitHub implementation's own behaviour is covered by
/// unit tests over its error translation.
/// </para>
/// <para>
/// The background poller is switched off here. A timer firing mid-assertion is a flake generator,
/// and the refresh endpoint drives exactly the same synchroniser.
/// </para>
/// </summary>
public sealed class BacklogApiFixture : ApiServiceFixtureBase
{
    internal StubBacklogConnector Vendor { get; } = new();

    /// <summary>Every StoryChanged the relay delivered — the observable artifact for event tests.</summary>
    internal RecordingStoryChangedHandler DeliveredEvents { get; } = new();

    /// <summary>Reads and writes the same dictionary, so a stored value is one that resolves.</summary>
    internal StubSecretVault Secrets { get; } = new();

    /// <summary>Who the caller is.</summary>
    internal StubPrincipal Caller { get; } = new();

    /// <summary>What they may do, so the Admin-only path can be exercised from both sides.</summary>
    internal StubPermissions Permissions { get; } = new();

    protected override string[] SchemasToReset => [BacklogDbContext.Schema];

    /// <summary>
    /// The stubs go back with the database. Every class already calls this; the three that changed
    /// the caller also called Reset themselves, which restored it before their own tests and not
    /// after their last one — so the Member role a refusal test set leaked into the next class in the
    /// collection. Harmless while one endpoint checked a role; eight failures once the pipeline did.
    /// </summary>
    public override async Task ResetDatabase()
    {
        await base.ResetDatabase();
        Caller.Reset();
        Permissions.Reset();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("Backlog:PollingEnabled", "false");

        builder.ConfigureTestServices(services =>
        {
            // A real consumer registered through the same extension a module would use, so the
            // tests exercise the full path: transactional publish → outbox → relay → handler.
            services.AddSingleton(DeliveredEvents);
            services.AddIntegrationEventHandler<StoryChanged, RecordingStoryChangedHandler.Proxy>();
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBacklogConnector>();
            services.AddSingleton<IBacklogConnector>(Vendor);
            services.RemoveAll<ISecretResolver>();
            services.AddSingleton<ISecretResolver>(Secrets);
            services.RemoveAll<ISecretStore>();
            services.AddSingleton<ISecretStore>(Secrets);
            services.RemoveAll<ICurrentPrincipal>();
            services.AddSingleton<ICurrentPrincipal>(Caller);
            services.RemoveAll<IProjectPermissions>();
            services.AddSingleton<IProjectPermissions>(Permissions);
        });
    }
}

/// <summary>A vendor whose responses the test decides, including how it fails.</summary>
sealed class StubBacklogConnector : IBacklogConnector
{
    public BacklogVendor Vendor => BacklogVendor.GitHub;

    public List<VendorStory> Stories { get; } = [];

    public Error? VerifyError { get; set; }

    public Error? FetchError { get; set; }

    public int FetchCount => _fetches;

    public void Reset()
    {
        Stories.Clear();
        VerifyError = null;
        FetchError = null;
        VerifiedToken = null;
        StoriesRefusal = null;
        DocumentsRefusal = null;
        ProbedDocumentPath = null;
        // Added with the widened verdict (#226) and cleared here for the reason this whole method
        // exists: a mutable stub in a shared fixture leaks into the next class otherwise.
        ProbedCapabilities = [];
        WriteRefusal = null;
        WriteError = null;
        Comments.Clear();
        WriteStateError = null;
        Change = null;
        Documents.Clear();
        Files.Clear();
        DocumentError = null;
        LastReadRef = null;
        StoryComments.Clear();
        RepositoryLabels.Clear();
        EnsureLabelError = null;
        // The listing knobs, cleared for the same reason as everything above — #215 added them
        // without a line here, which is the leak this method exists to prevent.
        DirectoryFiles = null;
        DirectorySubdirectories.Clear();
        ListDirectoryError = null;
        Interlocked.Exchange(ref _fetches, 0);
    }

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

    public Task<ErrorOr<IReadOnlyList<StoryComment>>> ReadComments(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        DateTimeOffset since,
        string token,
        CancellationToken cancellationToken
    ) =>
        Task.FromResult<ErrorOr<IReadOnlyList<StoryComment>>>(
            ErrorOrFactory.From<IReadOnlyList<StoryComment>>([
                .. StoryComments
                    .Where(entry =>
                        entry.StoryId == vendorStoryId && entry.Comment.CreatedAt >= since
                    )
                    .Select(entry => entry.Comment)
                    .OrderBy(comment => comment.CreatedAt),
            ])
        );

    /// <summary>The credential the last verification actually used (#124, design D3).</summary>
    public string? VerifiedToken { get; private set; }

    /// <summary>Refuses the Stories capability when set (#132).</summary>
    public Error? StoriesRefusal { get; set; }

    /// <summary>Refuses the documents capability when set (#132).</summary>
    public Error? DocumentsRefusal { get; set; }

    /// <summary>The document path the last probe asked for, so tests can assert D6's shape.</summary>
    public string? ProbedDocumentPath { get; private set; }

    /// <summary>Which capabilities the last probe was asked about (#226).</summary>
    public IReadOnlyList<ConnectorCapability> ProbedCapabilities { get; private set; } = [];

    /// <summary>A write the stub refuses, so the widened verdict has a failing case to assert.</summary>
    public Error? WriteRefusal { get; set; }

    public Task<CredentialVerdict> VerifyAccess(
        BacklogCoordinates coordinates,
        IReadOnlyList<ConnectorCapability> capabilities,
        string token,
        CancellationToken cancellationToken
    )
    {
        VerifiedToken = token;
        ProbedDocumentPath = ConnectorCapability.DocumentPath;
        ProbedCapabilities = capabilities;

        // VerifyError stays meaningful: a whole-credential refusal is the Stories one, which is
        // what every test written before capabilities existed was expressing.
        var stories = StoriesRefusal ?? VerifyError;

        var results = capabilities.Select(capability =>
        {
            if (capability == ConnectorCapability.ReadStories && stories is { } first)
            {
                return CapabilityResult.Refused(capability.Name, first);
            }

            if (capability == ConnectorCapability.ReadDocuments && DocumentsRefusal is { } second)
            {
                return CapabilityResult.Refused(capability.Name, second);
            }

            if (capability.IsWrite && WriteRefusal is { } third)
            {
                return CapabilityResult.Refused(capability.Name, third);
            }

            return CapabilityResult.Passed(capability.Name);
        });

        return Task.FromResult(new CredentialVerdict([.. results]));
    }

    public Task<ErrorOr<BacklogSnapshot>> FetchStories(
        BacklogCoordinates coordinates,
        string token,
        CancellationToken cancellationToken
    )
    {
        Interlocked.Increment(ref _fetches);

        return Task.FromResult(
            FetchError is { } error
                ? ErrorOrFactory.From<BacklogSnapshot>([error])
                : new BacklogSnapshot([.. Stories])
        );
    }

    public Error? WriteError { get; set; }

    public Task<ErrorOr<Success>> ApplyLabel(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string label,
        string token,
        CancellationToken cancellationToken
    ) =>
        Write(
            vendorStoryId,
            story =>
                story.Labels.Contains(label)
                    ? story
                    : story with
                    {
                        Labels = [.. story.Labels, label],
                    }
        );

    public Task<ErrorOr<Success>> RemoveLabel(
        BacklogCoordinates coordinates,
        string vendorStoryId,
        string label,
        string token,
        CancellationToken cancellationToken
    ) =>
        Write(
            vendorStoryId,
            story => story with { Labels = [.. story.Labels.Where(existing => existing != label)] }
        );

    /// <summary>The change the test says references a Story, or none.</summary>
    public LinkedChange? Change { get; set; }

    public Dictionary<string, string> Documents { get; } = new(StringComparer.Ordinal);

    public Error? DocumentError { get; set; }

    /// <summary>The ref the last content read used — the head-ref contract, observable.</summary>
    public string? LastReadRef { get; private set; }

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

    /// <summary>Files beyond the documents — set when a test cares about diffs.</summary>
    public List<ChangedFile> Files { get; } = [];

    public Task<ErrorOr<IReadOnlyList<ChangedFile>>> ListChangeFiles(
        BacklogCoordinates coordinates,
        int changeNumber,
        string token,
        CancellationToken cancellationToken
    )
    {
        if (DocumentError is { } error)
        {
            return Task.FromResult<ErrorOr<IReadOnlyList<ChangedFile>>>(error);
        }

        // Documents are markdown files, so a test that only set Documents still gets a
        // coherent files list — the same projection the product makes.
        IReadOnlyList<ChangedFile> files =
        [
            .. Documents
                .Keys.OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new ChangedFile(path, "modified", 1, 0, "@@ patch", null)),
            .. Files,
        ];

        return Task.FromResult<ErrorOr<IReadOnlyList<ChangedFile>>>(files.ToList());
    }

    public Task<ErrorOr<string>> ReadDocument(
        BacklogCoordinates coordinates,
        string path,
        string reference,
        string token,
        CancellationToken cancellationToken
    )
    {
        LastReadRef = reference;

        if (DocumentError is { } error)
        {
            return Task.FromResult<ErrorOr<string>>(error);
        }

        return Task.FromResult<ErrorOr<string>>(
            Documents.TryGetValue(path, out var content)
                ? content
                : BacklogErrors.DocumentNotFound(path)
        );
    }

    /// <summary>The listing the picker reads (#215); null means the directory is absent.</summary>
    public List<string>? DirectoryFiles { get; set; }

    /// <summary>The listing's child directories (#229) — empty unless a test needs one.</summary>
    public List<string> DirectorySubdirectories { get; } = [];

    /// <summary>Set to make the listing fail as the vendor would refuse it.</summary>
    public Error? ListDirectoryError { get; set; }

    public Task<ErrorOr<DirectoryEntries?>> ListDirectoryFiles(
        BacklogCoordinates coordinates,
        string path,
        string token,
        CancellationToken cancellationToken
    )
    {
        if (ListDirectoryError is { } error)
        {
            return Task.FromResult<ErrorOr<DirectoryEntries?>>(error);
        }

        DirectoryEntries? entries = DirectoryFiles is null
            ? null
            : new DirectoryEntries([.. DirectoryFiles], [.. DirectorySubdirectories]);
        return Task.FromResult(ErrorOrFactory.From(entries));
    }

    Task<ErrorOr<Success>> Write(string vendorStoryId, Func<VendorStory, VendorStory> mutate)
    {
        if (WriteError is { } error)
        {
            return Task.FromResult<ErrorOr<Success>>(error);
        }

        var index = Stories.FindIndex(story => story.VendorId == vendorStoryId);
        if (index >= 0)
        {
            Stories[index] = mutate(Stories[index]);
        }

        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }

    int _fetches;
}

/// <summary>
/// One dictionary behind both seams, so what a test stores is what the handler resolves. That
/// matters here: the paste path verifies with the value it reads back, and a stub that answered
/// resolution from thin air would let a store that never wrote look like one that did.
/// </summary>
sealed class StubSecretVault : ISecretResolver, ISecretStore
{
    readonly ConcurrentDictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <summary>Set to simulate a habitat with nowhere to put a value.</summary>
    public string? UnavailableRemedy { get; set; }

    public IReadOnlyDictionary<string, string> Stored => _values;

    public void Reset()
    {
        _values.Clear();
        UnavailableRemedy = null;
    }

    public Task<string> Resolve(string secretName, CancellationToken cancellationToken = default)
    {
        if (_values.TryGetValue(secretName, out var stored))
        {
            return Task.FromResult(stored);
        }

        // The named path keeps working exactly as it did: an unknown name that is not the
        // deliberately-missing one resolves to the same stub token these tests always used.
        return secretName == "missing-secret"
            ? throw new SecretNotFoundException(secretName)
            : Task.FromResult("stub-token");
    }

    public Task Store(
        string secretName,
        string value,
        CancellationToken cancellationToken = default
    )
    {
        if (UnavailableRemedy is not null)
        {
            throw new SecretStoreUnavailableException(UnavailableRemedy);
        }

        _values[secretName] = value;
        return Task.CompletedTask;
    }
}

/// <summary>The caller the host would otherwise decide.</summary>
sealed class StubPrincipal : ICurrentPrincipal
{
    public Principal Current { get; set; } = new("test-admin", "Test admin");

    public void Reset() => Current = new("test-admin", "Test admin");
}

/// <summary>
/// What the caller may do, so both sides of the pipeline's check run (#13). A separate stub from
/// the principal because that is now the separation being tested: who they are and what they may do
/// on a given project are two answers, and a test that could only set one could not describe a
/// Member.
/// </summary>
sealed class StubPermissions : IProjectPermissions
{
    public ProjectRole? Role { get; set; } = ProjectRole.Admin;

    public Task<ProjectRole?> RoleOn(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult(Role);

    public Task<IReadOnlySet<Guid>?> VisibleProjects(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>?>(null);

    public void Reset() => Role = ProjectRole.Admin;
}

[CollectionDefinition(Name)]
public sealed class BacklogCollection : ICollectionFixture<BacklogApiFixture>
{
    public const string Name = "Backlog";
}

/// <summary>
/// Collects delivered StoryChanged events. The handler itself is scoped (the relay creates a
/// scope per delivery), so it proxies into this singleton collector.
/// </summary>
sealed class RecordingStoryChangedHandler
{
    readonly ConcurrentQueue<StoryChanged> _events = new();

    /// <summary>
    /// Deliveries for one Project. Tests filter by their own Project id because the collection
    /// shares one host: another test's refresh may still be delivering when this one asserts.
    /// </summary>
    public IReadOnlyList<StoryChanged> For(Guid projectId) =>
        [.. _events.Where(@event => @event.ProjectId == projectId)];

    void Record(StoryChanged @event) => _events.Enqueue(@event);

    /// <summary>
    /// Waits for delivery: publish is transactional but delivery is asynchronous, so asserting
    /// immediately after the HTTP call races the dispatcher. Polling the artifact is honest;
    /// sleeping a fixed time is a flake.
    /// </summary>
    public async Task<IReadOnlyList<StoryChanged>> WaitForAtLeast(
        Guid projectId,
        int count,
        TimeSpan? timeout = null
    )
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));

        while (DateTime.UtcNow < deadline)
        {
            if (For(projectId).Count >= count)
            {
                break;
            }

            await Task.Delay(100);
        }

        return For(projectId);
    }

    internal sealed class Proxy(RecordingStoryChangedHandler collector)
        : IIntegrationEventHandler<StoryChanged>
    {
        public Task Handle(StoryChanged @event, CancellationToken cancellationToken)
        {
            collector.Record(@event);
            return Task.CompletedTask;
        }
    }
}
