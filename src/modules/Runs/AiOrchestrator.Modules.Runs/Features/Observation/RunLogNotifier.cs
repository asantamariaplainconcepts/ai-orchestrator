using AiOrchestrator.Modules.Runs.Domain;
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
    /// <summary>
    /// How far each Run has been pushed, so a notification sends only what is new.
    /// <para>
    /// Bounded by what is running (#144, design D4): an entry is removed once its Run reaches a
    /// terminal state, because a terminal Run produces no further output. Without that the map grew
    /// for the life of the process — slowly, invisibly, and forever.
    /// </para>
    /// </summary>
    readonly Dictionary<Guid, int> _sent = [];

    /// <summary>
    /// One gate per Run, so two notifications for the same Run cannot both read the cursor and push
    /// overlapping frames. Per Run rather than global on purpose: a single lock would make every
    /// Run's window wait behind every other, which is the opposite of what a live window is for.
    /// </summary>
    readonly Dictionary<Guid, SemaphoreSlim> _gates = [];

    /// <summary>Guards the two dictionaries themselves, which are touched from concurrent handlers.</summary>
    readonly SemaphoreSlim _bookkeeping = new(1, 1);

    async Task<SemaphoreSlim> GateFor(Guid runId, CancellationToken cancellationToken)
    {
        await _bookkeeping.WaitAsync(cancellationToken);
        try
        {
            if (!_gates.TryGetValue(runId, out var gate))
            {
                gate = new SemaphoreSlim(1, 1);
                _gates[runId] = gate;
            }

            return gate;
        }
        finally
        {
            _bookkeeping.Release();
        }
    }

    /// <summary>Releases a finished Run's bookkeeping. Idempotent — a Run may notify after ending.</summary>
    async Task Forget(Guid runId, CancellationToken cancellationToken)
    {
        await _bookkeeping.WaitAsync(cancellationToken);
        try
        {
            _sent.Remove(runId);
            if (_gates.Remove(runId, out var gate))
            {
                gate.Dispose();
            }
        }
        finally
        {
            _bookkeeping.Release();
        }
    }

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
        // Serialised per Run (#144, design D4): two notifications arriving together used to both
        // read the same cursor and push overlapping frames, so a watcher saw lines twice.
        var gate = await GateFor(runId, cancellationToken);
        await gate.WaitAsync(cancellationToken);

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

            // Read after the lines, and inside the gate: a Run that ended between the notification
            // and this read is one whose bookkeeping can go, and the state must not be read before
            // the lines or a final chunk would be dropped along with the cursor.
            // Derived from BR-001's own list rather than a second one: RunStates carries a warning
            // that hand-written copies of it have drifted twice, and an inverse copy would drift
            // identically. Read as a state, then judged in memory, because the complement of a
            // static array is not something to ask a query provider to translate.
            var states = await database
                .Runs.Where(run => run.Id == runId)
                .Select(run => run.State)
                .ToListAsync(cancellationToken);
            var terminal = states.Count == 1 && RunStates.IsTerminal(states[0]);

            if (chunks.Count == 0)
            {
                if (terminal)
                {
                    await Forget(runId, cancellationToken);
                }

                return;
            }

            _sent[runId] = chunks[^1].Sequence + 1;

            // The frame carries where it starts (#144, design D5). Without it a client that
            // subscribed before its first read cannot tell an overlap from new output, and
            // "subscribe first" would trade a gap for duplicated text — which is what the first
            // draft of that design assumed the client already handled, and it did not.
            await hub
                .Clients.Group(RunLogHub.GroupFor(runId))
                .SendAsync(
                    "lines",
                    new
                    {
                        from = chunks[0].Sequence,
                        lines = chunks.Select(chunk => chunk.Content),
                    },
                    cancellationToken: cancellationToken
                );

            if (terminal)
            {
                await Forget(runId, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            // Same contract as the writer's: the witness never kills the witnessed.
            PushFailed(logger, exception, runId);
        }
        finally
        {
            // Released whatever happened. A gate left held would silence that Run's window for the
            // life of the process, which is a worse failure than the one being handled above.
            gate.Release();
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
