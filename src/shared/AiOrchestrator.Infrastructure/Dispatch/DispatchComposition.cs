using AiOrchestrator.BuildingBlocks.Dispatch;
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
    /// The producer side: registers <see cref="IRunDispatcher"/> over the one substrate dispatch
    /// has (#225, reshaped by #296): the same durable Postgres outbox integration events use.
    /// <para>
    /// There used to be two, chosen by configuration presence — a Storage Queue with a KEDA-scaled
    /// worker for the cloud, the outbox everywhere else (DEC-013). The queue's reasons evaporated
    /// with the sandbox substrate: executing a Run stopped being heavy (it is an API call and a
    /// poll loop; the heavy half lives in the sandbox, which scales itself and bills nothing
    /// idle), and the worker's separate identity no longer stood between the portal and a
    /// root-equivalent socket. DEC-013 is superseded and the corpus says so.
    /// </para>
    /// <para>
    /// This registers a producer only. The consumer is composed by the host that should be able
    /// to execute Runs, never acquired by registering the producer (design D2).
    /// </para>
    /// </summary>
    public static TBuilder AddRunDispatch<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        RefuseRetiredQueue(builder);
        RequireOutbox(builder);
        // Depends on AddIntegrationEvents() having composed CAP — the outbox is the same one, on
        // purpose. Stated here because the failure otherwise arrives as a resolve-time DI error
        // naming ICapPublisher, which tells a reader nothing about the ordering they broke.
        builder.Services.AddSingleton<IRunDispatcher, OutboxRunDispatcher>();
        return builder;
    }

    /// <summary>
    /// The in-process consumer, for the host that both dispatches and executes — which since #296
    /// is the only arrangement: the worker whose queue this used to guard is retired.
    /// </summary>
    public static TBuilder AddRunDispatchConsumer<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        RefuseRetiredQueue(builder);
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

    /// <summary>
    /// The retired queue's connection name, kept only so a habitat still naming it is refused by
    /// name (#296) — the same treatment the retired pod image gets, for the same reason.
    /// </summary>
    public const string RetiredQueueConnectionName = "queues";

    static void RefuseRetiredQueue(IHostApplicationBuilder builder)
    {
        if (
            !string.IsNullOrWhiteSpace(
                builder.Configuration.GetConnectionString(RetiredQueueConnectionName)
            )
        )
        {
            throw new InvalidOperationException(
                $"This habitat names the '{RetiredQueueConnectionName}' connection string, a "
                    + "dispatch substrate that no longer exists. Runs used to travel a Storage "
                    + "Queue to a KEDA-scaled worker; they now dispatch through the Postgres "
                    + "outbox and execute from this process, whose launcher decides where the "
                    + "agent runs. Remove the connection string — and the queue, the worker job "
                    + "and its identity from the deployment, none of which anything reads now."
            );
        }
    }

    /// <summary>
    /// Dispatch needs the database the events' outbox already lives in. Named rather than
    /// assumed: nothing configured is an ambiguous contract, not a default.
    /// </summary>
    static void RequireOutbox(IHostApplicationBuilder builder)
    {
        if (
            string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("aiorchestratordb"))
        )
        {
            throw new InvalidOperationException(
                "'aiorchestratordb' is not configured, so this habitat has no dispatch substrate: "
                    + "Runs dispatch through the Postgres outbox, and the outbox lives in that "
                    + "database."
            );
        }
    }
}
