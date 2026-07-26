using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.IntegrationEvents;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Runs.Features.Execution;
using AiOrchestrator.Modules.Runs.Features.Matching;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Modules.Runs;

public sealed class RunsModule : ModuleBase
{
    public const string ConnectionStringName = "aiorchestratordb";

    /// <summary>
    /// BR-002's cap. "Configurable by Admin" is a later slice; until then the default of 2 is
    /// overridable per host, not hardcoded into the handler.
    /// </summary>
    public const string ProjectConcurrencyCapKey = "Runs:ProjectConcurrencyCap";

    /// <summary>
    /// Where the AI credential lives, by NAME (DEC-014 — the vault holds the value). Host
    /// configuration until Project configuration gains a per-project field (its own issue);
    /// the stated simplification is the name's scope, never its storage.
    /// </summary>
    public const string AiCredentialSecretNameKey = "Runs:AiCredentialSecretName";

    public override string Name => "Runs";

    public override async Task Migrate(
        IServiceProvider services,
        CancellationToken cancellationToken
    )
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
    }

    public override void Add(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<RunsDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString(ConnectionStringName),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", RunsDbContext.Schema);
                    npgsql.EnableRetryOnFailure();
                }
            )
        );

        services.AddHealthChecks().AddDbContextCheck<RunsDbContext>("runs-db");

        services.AddSingleton(
            new RunsOptions
            {
                ProjectConcurrencyCap = configuration.GetValue(
                    ProjectConcurrencyCapKey,
                    defaultValue: 2
                ),
                AiCredentialSecretName = configuration.GetValue(
                    AiCredentialSecretNameKey,
                    defaultValue: "anthropic-api-key"
                )!,
            }
        );

        // The worker-facing execution surface (agent-execution spec).
        services.AddScoped<IRunExecutor, RunExecutor>();

        // The one creation path both matching and Run now share (BR-013).
        services.AddScoped<RunCreator>();

        // The first consumer of the event stream: matching reacts to story changes.
        services.AddIntegrationEventHandler<StoryChanged, StoryChangedHandler>();
    }
}

/// <summary>Composed once per host from configuration; see <see cref="RunsModule"/> keys.</summary>
sealed class RunsOptions
{
    /// <summary>BR-002: max concurrent Runs in Planning/Executing per Project. Default 2.</summary>
    public required int ProjectConcurrencyCap { get; init; }

    /// <summary>See <see cref="RunsModule.AiCredentialSecretNameKey"/> — a name, never a value.</summary>
    public required string AiCredentialSecretName { get; init; }
}
