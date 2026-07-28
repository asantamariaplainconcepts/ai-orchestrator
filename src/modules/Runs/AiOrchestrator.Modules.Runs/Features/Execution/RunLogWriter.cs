using System.Threading.Channels;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
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

    internal static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(2);

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
        }
        catch (Exception exception)
        {
            // The witness must never kill the witnessed (class doc): the transcript at run end
            // still carries everything; only liveness is lost.
            LogWriteFailed(_logger, exception, _runId);
        }
    }

    static string Bounded(string line) => line.Length <= 8192 ? line : line[..8192];

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
