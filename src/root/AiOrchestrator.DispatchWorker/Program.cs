using AiOrchestrator.ServiceDefaults;
using AiOrchestrator.ServiceDefaults.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// The consuming end of the dispatch substrate. KEDA starts one of these per queued message and
// it exits when the queue is empty — there is no long-running loop and nothing scheduled.
//
// What it does with the Run is deliberately nothing yet: #17 gives Runs meaning and #18 gives
// them an Agent. This change proves only the mechanical claim — a message becomes a container
// that runs — because building Run semantics on an unproven substrate means debugging two
// unknowns at once.
var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddRunDispatchReader();

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DispatchWorker");
var reader = host.Services.GetRequiredService<DispatchQueueReader>();

// Drain rather than take one: KEDA's scaler and the queue length are eventually consistent, so a
// job that handled exactly one message would leave stragglers waiting for the next poll. The
// loop ends when the queue is empty, which is what makes the job exit and scale back to zero.
var handled = 0;

while (await reader.Claim() is { } runId)
{
    handled++;
    WorkerLog.Claimed(logger, runId);
}

WorkerLog.PassComplete(logger, handled);

/// <summary>
/// Source-generated log delegates. Required by CA1848 rather than chosen — and the event ids are
/// unique across the solution so a log query can select one call site unambiguously.
/// </summary>
static partial class WorkerLog
{
    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Claimed run {RunId}")]
    public static partial void Claimed(ILogger logger, Guid runId);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "Dispatch pass complete: {Handled} claimed"
    )]
    public static partial void PassComplete(ILogger logger, int handled);
}
