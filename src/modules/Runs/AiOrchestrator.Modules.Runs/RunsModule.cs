using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Dispatch;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.IntegrationEvents;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Runs.Features.Execution;
using AiOrchestrator.Modules.Runs.Features.Matching;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Runs;

public sealed class RunsModule : ModuleBase
{
    public const string ConnectionStringName = "aiorchestratordb";

    /// <summary>
    /// BR-002's cap. "Configurable by Admin" is a later slice; until then the default of 2 is
    /// overridable per host, not hardcoded into the handler.
    /// </summary>
    public const string ProjectConcurrencyCapKey = "Runs:ProjectConcurrencyCap";
    public const string ResumeCheckEnabledKey = "Runs:ResumeCheckEnabled";

    /// <summary>Opt-out for the live window (#106): delivery only, never the record.</summary>
    public const string LiveLogEnabledKey = "Runs:LiveLogEnabled";
    public const string ResumeCheckSecondsKey = "Runs:ResumeCheckSeconds";

    /// <summary>Set false in the test host, which drives the sweep deterministically (#140).</summary>
    public const string ReapingEnabledKey = "Runs:ReapingEnabled";

    public const string ReapIntervalSecondsKey = "Runs:ReapIntervalSeconds";

    public const string ReapGraceSecondsKey = "Runs:ReapGraceSeconds";

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
        // ACT-002's list, almost verbatim: view runs, logs and cost; trigger Run now (DEC-035);
        // approve plans (UC-011); cancel runs (UC-019). Dismissing a failure is the one addition, and
        // RunPermissions.DismissFailure says why.
        services.AddPermissionGrants(
            BuildingBlocks.Identity.ProjectRole.Member,
            RunPermissions.Read,
            RunPermissions.Trigger,
            RunPermissions.Approve,
            RunPermissions.Cancel,
            RunPermissions.DismissFailure,
            RunPermissions.HoldConversation,
            // The second addition to ACT-002's list, and the only one that executes anything: see
            // RunPermissions.Attach for the cost accepted in granting it to Members (#288, #304).
            RunPermissions.Attach
        );
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
                ReapInterval = TimeSpan.FromSeconds(
                    configuration.GetValue(ReapIntervalSecondsKey, defaultValue: 60)
                ),
                // Five minutes (#144, design D3). Both 120 and 300 are guesses; the asymmetry
                // decides. Too short races a worker about to finish and destroys real work; too
                // long leaves a Story hostage a few minutes more. Only the second is recoverable.
                ReapGrace = TimeSpan.FromSeconds(
                    configuration.GetValue(ReapGraceSecondsKey, defaultValue: 300)
                ),
            }
        );

        // Opt-out rather than opt-in, and composed here so the long-lived host carries it: the
        // dispatch worker scales to zero, and a sweep that only runs while workers run cannot
        // notice that no worker is running (#140, design D4). The functional test host disables
        // it and drives the sweep directly, because a timer firing mid-assertion is a flake
        // generator — the same reason the backlog poller is switched off there.
        services.AddScoped<RunReaping>();

        if (configuration.GetValue(ReapingEnabledKey, defaultValue: true))
        {
            services.AddHostedService<AbandonedRunReaper>();
        }

        // The runtimes' default answer is the same sentence one seam over (#279): a process
        // that does not execute Runs itself says so, rather than rendering runtimes it never
        // spawns as missing.
        services.TryAddSingleton<IAgentRuntimesMonitor, UnhostedAgentRuntimesMonitor>();

        // The model chooser's default (#291), one more time for the same reason: a process that
        // executes no Runs has no machine to ask, so it declares nothing rather than reporting a
        // failure to reach a machine that was never there.
        services.TryAddSingleton<IAgentModelCatalog, NoAgentModelCatalog>();

        // And previews, for the same reason again (run-previews): the read and the relay must
        // always resolve, so a habitat that holds no sandboxes answers "not hosted here" instead
        // of failing — the ability is absent, never the answer.
        services.TryAddSingleton<IRunPreviewMonitor, UnhostedRunPreviewMonitor>();

        // And sandboxes, once more (#304): the terminal's surface must always resolve, and a
        // habitat that launches none — a deployment, by ADR-0021 — answers "not hosted here"
        // rather than looking like a Run with no sandbox.
        services.TryAddSingleton<IRunSandboxMonitor, UnhostedRunSandboxMonitor>();
        services.TryAddSingleton<IRunTerminalHost, UnhostedRunTerminalHost>();

        // Who attached, and when, against the Run itself (#304 criterion 6) — the trace that makes
        // granting the capability to Members reasonable at all (#288).
        services.AddScoped<
            Features.Observation.IRunAttachRecorder,
            Features.Observation.RunAttachRecorder
        >();

        // The relay's own client, bounded: a preview that hangs must not hold a portal request
        // open, and the timeout is short because the target is loopback on this machine.
        services
            .AddHttpClient(nameof(Features.Observation.UseCases.RelayRunPreview))
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(10));

        // The worker-facing execution surface (agent-execution spec).
        services.AddScoped<IRunExecutor, RunExecutor>();

        // The one creation path both matching and Run now share (BR-013).
        services.AddScoped<RunCreator>();

        // The module's first published contract (#84): what other modules may ask about Runs.
        services.AddScoped<Contracts.IRunUsage, Features.Observation.RunUsage>();

        // Conversations (#78): the primitives every conversational action shares, and the
        // checker that wakes waiting Runs. Opt-out like the backlog poller — production checks,
        // the test host drives one pass deterministically instead.
        services.AddScoped<Features.Conversation.ConversationGate>();
        if (configuration.GetValue(ResumeCheckEnabledKey, defaultValue: true))
        {
            services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(
                provider => new Features.Conversation.ResumeChecker(
                    provider.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(),
                    TimeSpan.FromSeconds(
                        configuration.GetValue(ResumeCheckSecondsKey, defaultValue: 60)
                    ),
                    provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Features.Conversation.ResumeChecker>>()
                )
            );
        }

        // The live window (#106). The hub is delivery only — the durable record is the chunks
        // table, and the page's poll reads it whether or not any of this works. Opt-out like the
        // pollers above, because a functional test host has no viewers and no reason to hold a
        // listening connection.
        services.AddSignalR();
        if (configuration.GetValue(LiveLogEnabledKey, defaultValue: true))
        {
            services.AddHostedService<Features.Observation.RunLogNotifier>();
        }

        // The first consumer of the event stream: matching reacts to story changes.
        services.AddIntegrationEventHandler<StoryChanged, StoryChangedHandler>();
    }
}

/// <summary>Composed once per host from configuration; see <see cref="RunsModule"/> keys.</summary>
sealed class RunsOptions
{
    /// <summary>BR-002: max concurrent Runs in Planning/Executing per Project. Default 2.</summary>
    public required int ProjectConcurrencyCap { get; init; }

    /// <summary>How often abandoned Runs are swept for (#140). Never hardcoded at the call site.</summary>
    public required TimeSpan ReapInterval { get; init; }

    /// <summary>
    /// Added to a Run's deadline before it is considered abandoned. Exists so a worker that is
    /// finishing is never reaped mid-write; the conditional update is what actually guarantees it.
    /// </summary>
    public required TimeSpan ReapGrace { get; init; }
}
