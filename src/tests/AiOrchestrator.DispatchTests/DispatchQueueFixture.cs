using AiOrchestrator.ServiceDefaults.Dispatch;
using Azure.Storage.Queues;
using Testcontainers.Azurite;

namespace AiOrchestrator.DispatchTests;

/// <summary>
/// A real Azurite container — the same emulator the AppHost runs — so the enqueue → claim →
/// delete contract is exercised rather than mocked. A fake queue would prove nothing about the
/// one behaviour this change exists to guarantee: that a claimed message does not come back.
/// </summary>
public sealed class DispatchQueueFixture : IAsyncLifetime
{
    // Canonical registry name, matching the functional-test base. Behind a mirror, point
    // Testcontainers at it with TESTCONTAINERS_HUB_IMAGE_NAME_PREFIX rather than editing this.
    public const string AzuriteImage = "mcr.microsoft.com/azure-storage/azurite:3.35.0";

    readonly AzuriteContainer _azurite = new AzuriteBuilder(AzuriteImage).Build();

    public QueueServiceClient Queues { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _azurite.StartAsync();
        Queues = new QueueServiceClient(
            _azurite.GetConnectionString(),
            DispatchQueue.ClientOptions()
        );
    }

    /// <summary>Between tests: the queue must start empty or one test's leftovers become another's input.</summary>
    public async Task ResetQueue()
    {
        var queue = Queues.GetQueueClient(DispatchQueue.Name);
        await queue.CreateIfNotExistsAsync();
        await queue.ClearMessagesAsync();
    }

    public async Task<int> QueueDepth()
    {
        var queue = Queues.GetQueueClient(DispatchQueue.Name);
        await queue.CreateIfNotExistsAsync();
        var properties = await queue.GetPropertiesAsync();
        return properties.Value.ApproximateMessagesCount;
    }

    public Task DisposeAsync() => _azurite.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class DispatchQueueCollection : ICollectionFixture<DispatchQueueFixture>
{
    public const string Name = "DispatchQueue";
}
