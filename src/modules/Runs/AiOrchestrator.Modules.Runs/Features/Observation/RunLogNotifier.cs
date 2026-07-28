using AiOrchestrator.Modules.Runs.Features.Execution;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AiOrchestrator.Modules.Runs.Features.Observation;

/// <summary>
/// The portal's ear on the database (#106, design D1). One connection per replica holds
/// <c>LISTEN</c>; every committed batch of log chunks announces itself, and this pushes the new
/// lines to whoever is watching that Run.
/// <para>
/// The worker is not involved: it commits, Postgres announces, the portal reads and pushes. That
/// is what removes the per-Run ingest credential the original design needed, and what makes the
/// stream structurally a witness — a broken listener costs liveness and nothing else, because
/// the page's poll reads the same table.
/// </para>
/// </summary>
sealed partial class RunLogNotifier(
    IServiceScopeFactory scopes,
    IHubContext<RunLogHub> hub,
    ILogger<RunLogNotifier> logger
) : BackgroundService
{
    /// <summary>How far each Run has been pushed, so a notification sends only what is new.</summary>
    readonly Dictionary<Guid, int> _sent = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Listen(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // A dropped connection must not end the window forever; the poll covers the gap
                // while this waits and reconnects.
                ListenerFailed(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    async Task Listen(CancellationToken stoppingToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();

        // A dedicated physical connection: this one blocks on notifications, so it cannot be the
        // pooled connection the module's queries share.
        await using var connection = new NpgsqlConnection(database.Database.GetConnectionString());
        await connection.OpenAsync(stoppingToken);

        connection.Notification += (_, args) =>
        {
            if (Guid.TryParse(args.Payload, out var runId))
            {
                // Fire and forget on purpose: the handler must not block the notification loop,
                // and a failed push is a lost frame, never a lost line.
                _ = Push(runId, stoppingToken);
            }
        };

        await using (
            var listen = new NpgsqlCommand($"LISTEN {RunLogWriter.NotificationChannel}", connection)
        )
        {
            await listen.ExecuteNonQueryAsync(stoppingToken);
        }

        Listening(logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            await connection.WaitAsync(stoppingToken);
        }
    }

    async Task Push(Guid runId, CancellationToken cancellationToken)
    {
        try
        {
            var from = _sent.GetValueOrDefault(runId);

            await using var scope = scopes.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();

            var chunks = await database
                .LogChunks.Where(chunk => chunk.RunId == runId && chunk.Sequence >= from)
                .OrderBy(chunk => chunk.Sequence)
                .Select(chunk => new { chunk.Sequence, chunk.Content })
                .ToListAsync(cancellationToken);

            if (chunks.Count == 0)
            {
                return;
            }

            _sent[runId] = chunks[^1].Sequence + 1;

            await hub
                .Clients.Group(RunLogHub.GroupFor(runId))
                .SendAsync(
                    "lines",
                    chunks.Select(chunk => chunk.Content),
                    cancellationToken: cancellationToken
                );
        }
        catch (Exception exception)
        {
            // Same contract as the writer's: the witness never kills the witnessed.
            PushFailed(logger, exception, runId);
        }
    }

    [LoggerMessage(
        EventId = 6130,
        Level = LogLevel.Information,
        Message = "Listening for Run log notifications"
    )]
    private static partial void Listening(ILogger logger);

    [LoggerMessage(
        EventId = 6131,
        Level = LogLevel.Warning,
        Message = "The Run log listener dropped; reconnecting. The page's poll covers the gap"
    )]
    private static partial void ListenerFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 6132,
        Level = LogLevel.Warning,
        Message = "Run {RunId}: could not push new log lines to watchers"
    )]
    private static partial void PushFailed(ILogger logger, Exception exception, Guid runId);
}
