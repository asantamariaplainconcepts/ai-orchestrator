using System.Collections.Concurrent;
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

    protected override string[] SchemasToReset => [BacklogDbContext.Schema];

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
            services.AddSingleton<ISecretResolver>(new StubSecretResolver());
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
        WriteError = null;
        Interlocked.Exchange(ref _fetches, 0);
    }

    public Task<ErrorOr<Success>> VerifyAccess(
        BacklogCoordinates coordinates,
        string token,
        CancellationToken cancellationToken
    ) =>
        Task.FromResult(
            VerifyError is { } error ? ErrorOrFactory.From<Success>([error]) : Result.Success
        );

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

sealed class StubSecretResolver : ISecretResolver
{
    public Task<string> Resolve(string secretName, CancellationToken cancellationToken = default) =>
        secretName == "missing-secret"
            ? throw new SecretNotFoundException(secretName)
            : Task.FromResult("stub-token");
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
