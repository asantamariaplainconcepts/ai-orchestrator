using AiOrchestrator.BuildingBlocks.Dispatch;
using Azure.Identity;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiOrchestrator.ServiceDefaults.Dispatch;

/// <summary>
/// Host composition for dispatch. An <c>IHostApplicationBuilder</c> extension, so a module
/// structurally cannot call it — the same barrier that keeps cloud SDKs out of modules.
/// </summary>
public static class DispatchComposition
{
    /// <summary>The producer side: registers <see cref="IRunDispatcher"/> over the queue.</summary>
    public static TBuilder AddRunDispatch<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        AddPinnedQueueClient(builder);
        builder.Services.AddSingleton<IRunDispatcher, QueueRunDispatcher>();
        return builder;
    }

    /// <summary>The consumer side, for the worker process.</summary>
    public static TBuilder AddRunDispatchReader<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        AddPinnedQueueClient(builder);
        builder.Services.AddSingleton<DispatchQueueReader>();
        return builder;
    }

    /// <summary>
    /// Registers the client directly rather than through Aspire's
    /// <c>AddAzureQueueServiceClient</c>.
    /// <para>
    /// Not a preference — a constraint. <see cref="DispatchQueue.PinnedApiVersion"/> has to reach
    /// the client, and <c>QueueClientOptions.Version</c> is constructor-only, so the integration's
    /// "configure the options object" callback cannot set it. The cost is losing that
    /// integration's queue health check and pre-wired telemetry; the alternative was an unpinned
    /// client speaking a protocol the emulator cannot serve, which would quietly hollow out every
    /// local and functional test of this substrate.
    /// </para>
    /// </summary>
    static void AddPinnedQueueClient(IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(
            DispatchQueue.ConnectionName
        );

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{DispatchQueue.ConnectionName}' is missing. Dispatch cannot "
                    + "work without a queue, and failing at startup is better than failing on the "
                    + "first Run."
            );
        }

        builder.Services.AddSingleton(Create(connectionString));
    }

    /// <summary>
    /// One setting, two shapes — chosen by what the value <i>is</i>, not by environment name.
    /// <para>
    /// Deployed, the queue endpoint arrives as a URI and the identity supplies the credential:
    /// there is no key to configure, which is the point (BR-010). Locally, Azurite hands Aspire a
    /// keyed connection string. Passing a URI to the connection-string constructor throws, and
    /// only at the first dispatch — so the discrimination happens here, at startup, where it is
    /// visible.
    /// </para>
    /// </summary>
    static QueueServiceClient Create(string setting) =>
        Uri.TryCreate(setting, UriKind.Absolute, out var endpoint)
        && endpoint.Scheme is "http" or "https"
            ? new QueueServiceClient(
                endpoint,
                new DefaultAzureCredential(),
                DispatchQueue.ClientOptions()
            )
            : new QueueServiceClient(setting, DispatchQueue.ClientOptions());
}
