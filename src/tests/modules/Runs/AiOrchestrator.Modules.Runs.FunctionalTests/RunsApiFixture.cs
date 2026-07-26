using System.Collections.Concurrent;
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

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBacklogConnector>();
            services.AddSingleton<IBacklogConnector>(Vendor);
            services.AddSingleton<ISecretResolver>(new StubSecretResolver());

            services.AddSingleton(Probe);
            services.AddIntegrationEventHandler<StoryChanged, DeliveryProbe.Handler>();
        });
    }
}

/// <summary>A vendor whose responses the test decides.</summary>
sealed class StubBacklogConnector : IBacklogConnector
{
    public BacklogVendor Vendor => BacklogVendor.GitHub;

    public List<VendorStory> Stories { get; } = [];

    public void Reset() => Stories.Clear();

    public Task<ErrorOr<Success>> VerifyAccess(
        BacklogCoordinates coordinates,
        string token,
        CancellationToken cancellationToken
    ) => Task.FromResult<ErrorOr<Success>>(Result.Success);

    public Task<ErrorOr<BacklogSnapshot>> FetchStories(
        BacklogCoordinates coordinates,
        string token,
        CancellationToken cancellationToken
    ) => Task.FromResult<ErrorOr<BacklogSnapshot>>(new BacklogSnapshot([.. Stories]));
}

sealed class StubSecretResolver : ISecretResolver
{
    public Task<string> Resolve(string secretName, CancellationToken cancellationToken = default) =>
        Task.FromResult("stub-token");
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

[CollectionDefinition(Name)]
public sealed class RunsCollection : ICollectionFixture<RunsApiFixture>
{
    public const string Name = "Runs";
}
