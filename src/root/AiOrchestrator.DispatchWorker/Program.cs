using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.ServiceDefaults;
using AiOrchestrator.ServiceDefaults.Agents;
using AiOrchestrator.ServiceDefaults.Dispatch;
using AiOrchestrator.ServiceDefaults.IntegrationEvents;
using AiOrchestrator.ServiceDefaults.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// The consuming end of the dispatch substrate. KEDA starts one of these per queued message and
// it exits when the queue is empty — there is no long-running loop and nothing scheduled.
//
// Since agent-runtime-seam (#18) the worker is a full host like the Server: it composes the
// modules, so a claimed Run id becomes an executed Run through the module's own surface
// (IRunExecutor) — the worker adds claiming and process lifetime, never Run semantics.
var builder = Host.CreateApplicationBuilder(args);

// The worker never polls the backlog; the portal host owns that. Set before module Add()s read
// configuration.
builder.Configuration["Backlog:PollingEnabled"] = "false";

builder.AddServiceDefaults();
builder.AddSecretResolution();
builder.AddIntegrationEvents();
builder.AddRunDispatchReader();
builder.AddAgentRuntime();

var modules = ModuleRegistration.Discover();
builder.Services.AddModules(modules, builder.Configuration);

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

    // A scope per Run: the executor takes DbContexts, and one Run's failure must not leak
    // tracked state into the next.
    await using var scope = host.Services.CreateAsyncScope();
    var executor = scope.ServiceProvider.GetRequiredService<IRunExecutor>();
    await executor.Execute(runId);
}

WorkerLog.PassComplete(logger, handled);

/// <summary>
/// Source-generated log delegates. Required by CA1848 rather than chosen — and the event ids are
/// unique across the solution so a log query can select one call site unambiguously.
/// </summary>
static partial class WorkerLog
{
    [LoggerMessage(EventId = 6001, Level = LogLevel.Information, Message = "Claimed run {RunId}")]
    public static partial void Claimed(ILogger logger, Guid runId);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Information,
        Message = "Dispatch pass complete: {Handled} claimed"
    )]
    public static partial void PassComplete(ILogger logger, int handled);
}
