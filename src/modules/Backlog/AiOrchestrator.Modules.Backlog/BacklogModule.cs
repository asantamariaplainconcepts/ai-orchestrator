using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Backlog.Features.Backlog;
using AiOrchestrator.Modules.Backlog.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Modules.Backlog;

public sealed class BacklogModule : ModuleBase
{
    public const string ConnectionStringName = "aiorchestratordb";

    /// <summary>
    /// Set false to compose the module without its background poller — which the functional test
    /// host does, so tests drive the deterministic refresh path instead of racing a timer.
    /// </summary>
    public const string PollingEnabledKey = "Backlog:PollingEnabled";

    public override string Name => "Backlog";

    public override async Task Migrate(
        IServiceProvider services,
        CancellationToken cancellationToken
    )
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<BacklogDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
    }

    public override void Add(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<BacklogDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString(ConnectionStringName),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", BacklogDbContext.Schema);
                    npgsql.EnableRetryOnFailure();
                }
            )
        );

        services.AddHealthChecks().AddDbContextCheck<BacklogDbContext>("backlog-db");

        // The vendor seam. A second implementation registers alongside; nothing else changes.
        services.AddSingleton<IGitHubClientFactory, GitHubClientFactory>();
        services.AddScoped<IBacklogConnector, GitHubBacklogConnector>();

        services.AddScoped<BacklogSynchroniser>();
        services.AddScoped<ConnectorAccess>();
        services.AddScoped<LabelWriteBack>();

        // The Contracts read surface — the owner registers its own implementation.
        services.AddScoped<IStoryReader, StoryReader>();
        services.AddScoped<IConnectorReader, ConnectorReader>();
        services.AddScoped<IChangeFileReader, ChangeFileReader>();

        var gitHubBaseAddress = configuration.GetValue<string?>("Backlog:GitHub:BaseAddress");
        var options = new BacklogOptions
        {
            PollInterval = TimeSpan.FromSeconds(
                configuration.GetValue("Backlog:PollIntervalSeconds", 60)
            ),
            GitHubBaseAddress = string.IsNullOrWhiteSpace(gitHubBaseAddress)
                ? null
                : new Uri(gitHubBaseAddress),
        };
        services.AddSingleton(options);

        // Opt-out rather than opt-in: production polls, the test host disables it explicitly.
        if (configuration.GetValue(PollingEnabledKey, defaultValue: true))
        {
            services.AddHostedService<BacklogPoller>();
        }
    }
}
