using System.Threading.Channels;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.Modules.Runs.Features.Execution;

/// <summary>
/// Owns the path from a process thread's output line to a committed chunk (#96, design D2).
/// <para>
/// <see cref="Write"/> is safe from any thread and never blocks the process: lines land in a
/// bounded channel and one task drains it in batches — up to <see cref="BatchSize"/> lines or
/// <see cref="FlushInterval"/>, whichever first — into its own scope. That budget is half the
/// stated ≤5s lag; the page's poll is the other half.
/// </para>
/// <para>
/// Log writing must never fail a Run: a full channel drops the oldest lines (the transcript in
/// <c>AgentResult.Log</c> remains the complete record at the end), and a write failure is
/// logged and swallowed. The stream is a witness, not a participant.
/// </para>
/// </summary>
sealed partial class RunLogWriter : IAsyncDisposable
{
    internal const int BatchSize = 50;

    /// <summary>
    /// The whole latency budget since #106: with the portal listening to Postgres rather than
    /// polling, a line reaches a watcher at most one flush after the runtime emitted it. Not
    /// lower, because a chatty half-hour Run already moves from ~900 commits to ~3,600 here and
    /// 100ms would quadruple that for a difference nobody can perceive.
    /// </summary>
    internal static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// The channel every portal replica listens on. One channel for all Runs, with the Run id as
    /// the payload: a listener that had to subscribe per Run would need to know which Runs exist,
    /// which is exactly the coupling this avoids.
    /// </summary>
    internal const string NotificationChannel = "run_log_appended";

    readonly Guid _runId;
    readonly IServiceScopeFactory _scopes;
    readonly ILogger _logger;
    readonly Channel<string> _lines;
    readonly Task _drain;
    int _sequence;

    public RunLogWriter(Guid runId, IServiceScopeFactory scopes, ILogger logger)
    {
        _runId = runId;
        _scopes = scopes;
        _logger = logger;
        _lines = Channel.CreateBounded<string>(
            new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest }
        );
        _drain = Task.Run(Drain);
    }

    /// <summary>Thread-safe, non-blocking; called from the process's output threads.</summary>
    public void Write(string line) => _lines.Writer.TryWrite(line);

    async Task Drain()
    {
        var batch = new List<string>(BatchSize);

        while (await _lines.Reader.WaitToReadAsync())
        {
            batch.Clear();
            var deadline = DateTimeOffset.UtcNow + FlushInterval;

            while (
                batch.Count < BatchSize
                && DateTimeOffset.UtcNow < deadline
                && _lines.Reader.TryRead(out var line)
            )
            {
                batch.Add(line);
            }

            if (batch.Count == 0)
            {
                // Nothing readable yet: wait out the flush interval rather than spinning.
                await Task.Delay(FlushInterval);
                continue;
            }

            await Flush(batch);
        }

        // The channel completed: whatever remains is the tail, flushed before dispose returns.
        batch.Clear();
        while (_lines.Reader.TryRead(out var line))
        {
            batch.Add(line);
            if (batch.Count == BatchSize)
            {
                await Flush(batch);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await Flush(batch);
        }
    }

    async Task Flush(List<string> batch)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<RunsDbContext>();

            foreach (var line in batch)
            {
                database.LogChunks.Add(
                    new RunLogChunk(_runId, _sequence++, Bounded(line), DateTimeOffset.UtcNow)
                );
            }

            await database.SaveChangesAsync();

            // Announce after the commit, so a listener that reacts instantly still finds the
            // rows. Postgres delivers NOTIFY at commit anyway; sending it inside the same
            // transaction would be a promise about rows that might yet roll back.
            // pg_notify rather than NOTIFY: the statement form takes no parameters, so the
            // channel and payload would have to be interpolated into SQL.
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_notify({NotificationChannel}, {_runId.ToString()})"
            );
        }
        catch (Exception exception)
        {
            // The witness must never kill the witnessed (class doc): the transcript at run end
            // still carries everything; only liveness is lost.
            LogWriteFailed(_logger, exception, _runId);
        }
    }

    /// <summary>
    /// The longest line a chunk can hold — the column's own width, named once so the two cannot
    /// drift (<see cref="Persistence.RunsDbContext"/> configures the column from this).
    /// </summary>
    internal const int MaxLineLength = 8192;

    /// <summary>
    /// What a cut line ends with, so the loss is a fact the reader can state rather than one it
    /// has to infer.
    /// </summary>
    internal const string TruncationMarker = "…[truncated]";

    /// <summary>
    /// Lines that outgrow the column are cut — an agent event embedding a whole file routinely
    /// does, so truncation is inevitable here and not a defect.
    /// <para>
    /// Being <b>silent</b> about it was. A cut JSON line can never parse, so the transcript reader
    /// fell back to its verbatim branch and rendered 8 KB of unparseable JSON on one line — the
    /// exact opposite of the readable transcript that screen exists to be, and with no hint that
    /// anything had been removed. The marker says what happened, which is what lets the reader
    /// recover the event's shape from the surviving prefix instead of dumping it.
    /// </para>
    /// </summary>
    static string Bounded(string line) =>
        line.Length <= MaxLineLength
            ? line
            : line[..(MaxLineLength - TruncationMarker.Length)] + TruncationMarker;

    public async ValueTask DisposeAsync()
    {
        _lines.Writer.TryComplete();
        await _drain;
    }

    [LoggerMessage(
        EventId = 6120,
        Level = LogLevel.Warning,
        Message = "Run {RunId}: a log batch could not be persisted; live view loses these lines"
    )]
    private static partial void LogWriteFailed(ILogger logger, Exception exception, Guid runId);
}
