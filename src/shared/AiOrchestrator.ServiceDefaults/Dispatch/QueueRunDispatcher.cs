using AiOrchestrator.BuildingBlocks.Dispatch;
using Azure.Storage.Queues;

namespace AiOrchestrator.ServiceDefaults.Dispatch;

/// <summary>
/// Azure Storage Queue implementation of the dispatch seam (DEC-013). Lives in ServiceDefaults,
/// not BuildingBlocks, because modules reference BuildingBlocks and none of them may reach a
/// cloud SDK — the same placement the Key Vault resolver has.
/// <para>
/// Locally this runs against Azurite through the identical client, so the enqueue → claim →
/// delete contract is exercised on every developer machine rather than mocked.
/// </para>
/// </summary>
public sealed class QueueRunDispatcher(QueueServiceClient service) : IRunDispatcher
{
    public async Task Dispatch(Guid runId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(runId, Guid.Empty);

        var queue = service.GetQueueClient(DispatchQueue.Name);

        // Idempotent and cheap; it also means a fresh Azurite or a fresh environment works without
        // a separate provisioning step for the local path.
        await queue.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        await queue.SendMessageAsync(
            DispatchMessage.For(runId).Serialise(),
            cancellationToken: cancellationToken
        );
    }
}

/// <summary>The one place the queue's name is written; the KEDA scale rule must match it.</summary>
public static class DispatchQueue
{
    public const string Name = "run-dispatch";

    /// <summary>Aspire connection name for the storage account holding it.</summary>
    public const string ConnectionName = "queues";

    /// <summary>
    /// The storage API version both Azure and our pinned Azurite speak.
    /// <para>
    /// The SDK defaults to the newest version it knows, which Azurite 3.35 rejects outright with
    /// HTTP 400. Pinning is not merely a way to make the emulator stop complaining: if production
    /// spoke a protocol the emulator cannot serve, the local and functional tiers would be
    /// exercising a different wire contract from the one that ships, and their green would mean
    /// less than it appears to. Same version everywhere, or the tests prove less.
    /// </para>
    /// <para>
    /// Determined by probing, not assumed — Azurite answers 400 for a version it does not
    /// support and 403 (auth) for one it does. Raise this when the Azurite image is raised, and
    /// re-probe rather than guessing.
    /// </para>
    /// </summary>
    public const QueueClientOptions.ServiceVersion PinnedApiVersion = QueueClientOptions
        .ServiceVersion
        .V2025_11_05;

    public static QueueClientOptions ClientOptions() => new(PinnedApiVersion);
}
