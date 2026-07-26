using AiOrchestrator.BuildingBlocks.IntegrationEvents;
using DotNetCore.CAP;
using DotNetCore.CAP.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Savorboard.CAP.InMemoryMessageQueue;

namespace AiOrchestrator.ServiceDefaults.IntegrationEvents;

/// <summary>
/// Host composition for integration events. An <c>IHostApplicationBuilder</c> extension — the
/// structural barrier that keeps modules from calling it, same as secrets and dispatch.
/// </summary>
public static class IntegrationEventComposition
{
    public const string ConnectionName = "aiorchestratordb";

    /// <summary>CAP's storage schema. Created by the MigrationService, not by apps (design D5).</summary>
    public const string Schema = "cap";

    public static TBuilder AddIntegrationEvents<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionName}' is missing. Integration events cannot "
                    + "work without their outbox, and failing at startup beats losing events."
            );
        }

        builder.Services.AddSingleton<CapIntegrationEventRelay>();
        builder.Services.AddSingleton<IIntegrationEventPublisher, CapIntegrationEventPublisher>();

        builder.Services.AddCap(cap =>
        {
            cap.UsePostgreSql(postgres =>
            {
                postgres.ConnectionString = connectionString;
                postgres.Schema = Schema;
            });
            cap.UseInMemoryMessageQueue();

            // Deliberate, small — not the ~50 default nobody chose (design D4). Retrying a
            // failed handler is legitimate (BR-004 governs Runs, not handlers), but unbounded
            // isn't a policy, it's an accident.
            cap.FailedRetryCount = 3;
            cap.FailedRetryInterval = 60;

            // How long after a restart the fallback processor waits before redelivering
            // in-flight work (spike: default 240s made redelivery look broken in short tests).
            cap.FallbackWindowLookbackSeconds = 60;

            // The spike's sharpest finding: an exhausted message is terminal and SILENT by
            // default — the telemetry-shrug shape. This callback is the floor of observability;
            // #17 owns surfacing it properly.
            cap.FailedThresholdCallback = failed =>
            {
                var logger = failed
                    .ServiceProvider.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(nameof(CapIntegrationEventRelay));
                RelayLog.DeadLettered(logger, failed.Message.GetName() ?? "(unnamed)");
            };
        });

        return builder;
    }
}
