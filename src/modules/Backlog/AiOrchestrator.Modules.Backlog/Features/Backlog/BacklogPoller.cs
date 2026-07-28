using AiOrchestrator.Modules.Backlog.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog;

/// <summary>
/// UC-009's scheduled half. Polls every configured Connector on the interval (BR-015, DEC-028).
/// <para>
/// Three properties are deliberate. It does not delay startup — the first pass happens after the
/// first interval, not during boot. It tolerates a Connector disappearing mid-loop, because
/// configuration changes while this runs. And it is <b>opt-in</b>: the host enables it, so the
/// functional test host never starts it. A background loop firing during tests is a flake
/// generator, and the deterministic refresh endpoint covers the same code.
/// </para>
/// </summary>
sealed partial class BacklogPoller(
    IServiceScopeFactory scopeFactory,
    BacklogOptions options,
    ILogger<BacklogPoller> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PollAll(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // One bad pass must never take the poller down; the next tick tries again.
                LogPassFailed(logger, exception);
            }
        }
    }

    async Task PollAll(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<BacklogDbContext>();

        var projectIds = await database
            .Connectors.Select(connector => connector.ProjectId)
            .ToListAsync(cancellationToken);

        foreach (var projectId in projectIds)
        {
            // A fresh scope per project: one project's failure must not poison another's context.
            await using var projectScope = scopeFactory.CreateAsyncScope();

            // An archived Project is not polled (#121). Asked per pass rather than filtered when
            // the list was read: a Project archived mid-pass should stop being polled now, not
            // on the next tick.
            var projects =
                projectScope.ServiceProvider.GetRequiredService<Projects.Contracts.IProjectCatalog>();
            if (!await projects.AcceptsWork(projectId, cancellationToken))
            {
                continue;
            }

            var synchroniser =
                projectScope.ServiceProvider.GetRequiredService<BacklogSynchroniser>();

            // Synchronise records its own failures against the Connector; nothing to do here but
            // keep going. A Connector deleted since the list was read simply reports not-found.
            await synchroniser.Synchronise(projectId, cancellationToken);
        }
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Error, Message = "Backlog poll pass failed")]
    static partial void LogPassFailed(ILogger logger, Exception exception);
}

/// <summary>Tunables for the Backlog module. The interval default comes from DEC-028.</summary>
sealed class BacklogOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The GitHub API root. Null means github.com. A value here points the client at a
    /// GitHub Enterprise Server instance — the same knob the E2E lane uses to stand a stub in
    /// front of Octokit, so that tier exercises the real client rather than a fake of it.
    /// </summary>
    public Uri? GitHubBaseAddress { get; init; }
}
