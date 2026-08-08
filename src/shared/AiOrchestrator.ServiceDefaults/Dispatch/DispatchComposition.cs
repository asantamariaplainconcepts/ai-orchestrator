using AiOrchestrator.BuildingBlocks.Dispatch;
using Azure.Identity;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AiOrchestrator.ServiceDefaults.Dispatch;

/// <summary>
/// Host composition for dispatch. An <c>IHostApplicationBuilder</c> extension, so a module
/// structurally cannot call it — the same barrier that keeps cloud SDKs out of modules.
/// </summary>
public static class DispatchComposition
{
    /// <summary>
    /// The producer side: registers <see cref="IRunDispatcher"/> over whichever substrate this
    /// habitat provides (#225, design D1).
    /// <para>
    /// Chosen by <b>configuration presence</b> and never by an environment name — ADR-0010's rule,
    /// and DEC-049's compose defaults to Production, so gating on an environment once refused to
    /// start the very habitat it protected. A queue connection string means the queue; its absence
    /// means the outbox. Both, or neither, is an ambiguous contract and refuses here, at startup,
    /// where it is visible.
    /// </para>
    /// <para>
    /// This registers a producer only. The outbox substrate's consumer is composed by the host
    /// that should be able to execute Runs, never by this call (design D2): the dispatch worker
    /// must not acquire one, and neither must a portal that has a queue.
    /// </para>
    /// </summary>
    public static TBuilder AddRunDispatch<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        if (HasQueue(builder))
        {
            AddPinnedQueueClient(builder);
            builder.Services.AddSingleton<IRunDispatcher, QueueRunDispatcher>();
            return builder;
        }

        RequireOutbox(builder);
        // Depends on AddIntegrationEvents() having composed CAP — the outbox is the same one, on
        // purpose. Stated here because the failure otherwise arrives as a resolve-time DI error
        // naming ICapPublisher, which tells a reader nothing about the ordering they broke.
        builder.Services.AddSingleton<IRunDispatcher, OutboxRunDispatcher>();
        return builder;
    }

    /// <summary>
    /// The in-process consumer, for a host that both dispatches and executes. Refuses where a
    /// queue exists: composing it there would give the portal the ability the dispatch identity
    /// exists to keep separate, and a mistake that silently widens a credential boundary is
    /// exactly the kind this refusal is cheap insurance against.
    /// </summary>
    public static TBuilder AddRunDispatchConsumer<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        if (HasQueue(builder))
        {
            throw new InvalidOperationException(
                $"Connection string '{DispatchQueue.ConnectionName}' is configured, so Runs are "
                    + "dispatched to a queue and executed by the worker. A host that also consumed "
                    + "them in-process would hold both sides of a boundary that exists so one "
                    + "compromise cannot reach both. Remove the consumer, or remove the queue."
            );
        }

        RequireOutbox(builder);
        builder.Services.AddSingleton<OutboxRunSubscriber>();

        // WHERE the claimed Run executes. The pod substrate is gone (#296): it put each Run in a
        // container launched over the docker socket, a grant its own requirement called
        // root-equivalent on the host, into a container sharing that host's kernel. What replaced
        // it is a sandbox launcher, selected in AgentSandboxComposition — so this composition no
        // longer decides where an agent runs, only that a claimed Run is handled in this process.
        builder.Services.AddSingleton<IDispatchedRunHandler, InProcessRunHandler>();

        // The runtimes this process could spawn are worth probing wherever the handler lives in
        // it (#279): the ledger the probe writes and the monitor registration the panel reads.
        // Registered after the module's unhosted default, deliberately — the later one resolves.
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<Agents.AgentRuntimesHost>();
        builder.Services.AddSingleton<BuildingBlocks.Agents.IAgentRuntimesMonitor>(provider =>
            provider.GetRequiredService<Agents.AgentRuntimesHost>()
        );
        builder.Services.AddHostedService<Agents.AgentRuntimesProbe>();

        // A habitat still naming a pod image is refused rather than silently ignored: an operator
        // upgrading needs the sentence more than the error, and a key that quietly stopped meaning
        // anything is how a deployment ends up running something nobody chose.
        var podImage = builder.Configuration.GetValue<string?>(PodImageKey);
        if (!string.IsNullOrWhiteSpace(podImage))
        {
            throw new InvalidOperationException(
                $"This habitat names '{PodImageKey}', a substrate that no longer exists. Each Run "
                    + "used to execute in a container launched over the docker socket; it now runs "
                    + "in a per-Run microVM, and no socket is granted anywhere. Name a sandbox "
                    + $"launcher instead ('{Agents.AgentSandboxComposition.LauncherKey}' = "
                    + $"'{Agents.AgentSandboxComposition.SbxLauncher}' on a machine you own, "
                    + $"'{Agents.AgentSandboxComposition.AcaLauncher}' in a deployment), and remove "
                    + "the docker socket grant from your compose."
            );
        }

        return builder;
    }

    /// <summary>
    /// The retired substrate's key, kept only so a habitat still naming it is refused by name
    /// (#296). A key that quietly stopped meaning anything is how a deployment ends up running
    /// something nobody chose.
    /// </summary>
    public const string PodImageKey = "Dispatch:PodImage";

    static bool HasQueue(IHostApplicationBuilder builder) =>
        !string.IsNullOrWhiteSpace(
            builder.Configuration.GetConnectionString(DispatchQueue.ConnectionName)
        );

    /// <summary>
    /// The outbox substrate needs the database the events' outbox already lives in. Named rather
    /// than assumed: neither substrate configured is an ambiguous contract, not a default.
    /// </summary>
    static void RequireOutbox(IHostApplicationBuilder builder)
    {
        if (
            string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("aiorchestratordb"))
        )
        {
            throw new InvalidOperationException(
                $"Neither '{DispatchQueue.ConnectionName}' nor 'aiorchestratordb' is configured, "
                    + "so this habitat names no dispatch substrate at all. Set the queue "
                    + "connection string to dispatch to a queue, or the database one to dispatch "
                    + "through the outbox — picking one silently is how a habitat ends up running "
                    + "a substrate nobody chose."
            );
        }
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
