using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.ServiceDefaults;
using AiOrchestrator.ServiceDefaults.Agents;
using AiOrchestrator.ServiceDefaults.Dispatch;
using AiOrchestrator.ServiceDefaults.IntegrationEvents;
using AiOrchestrator.ServiceDefaults.Secrets;
using Microsoft.Extensions.Configuration;
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

// The per-Run entry mode (#246): `--run <id>` executes exactly that Run and exits — the pod
// arrangement's whole contract, and no queue is read or even configured. The id is parsed
// before composition because a pod started with garbage should die naming it, not compose a
// host first.
Guid? singleRun = null;
var runFlag = Array.IndexOf(args, "--run");
if (runFlag >= 0)
{
    if (runFlag + 1 >= args.Length || !Guid.TryParse(args[runFlag + 1], out var parsed))
    {
        Console.Error.WriteLine("--run requires a Run id (a GUID). Usage: --run <id>");
        return 64;
    }

    singleRun = parsed;
}

// Refuse to start without the database — checked BEFORE anything composes, because a missing
// connection string thrown from inside composition proved able to leave the process alive and
// spinning after the unhandled exception (#246, observed): a pod that hangs instead of exiting
// non-zero holds a cap slot forever. Exit 69 (EX_UNAVAILABLE), deterministically.
//
// This asserts configuration, not reachability: a wrong password still fails later, at the
// first query. Claiming needs only the queue, so a worker missing its connection string is
// worse than a broken one — it would take messages, execute nothing, and exit zero (#90).
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("aiorchestratordb")))
{
    Console.Error.WriteLine(
        "The dispatch worker has no 'aiorchestratordb' connection string. Executing a Run is "
            + "database work, so the worker needs it exactly as the portal does — set "
            + "ConnectionStrings__aiorchestratordb (or the SecretName variant on the job)."
    );
    return 69;
}

// The worker never polls the backlog; the portal host owns that. Set before module Add()s read
// configuration.
builder.Configuration["Backlog:PollingEnabled"] = "false";

builder.AddServiceDefaults();
builder.AddSecretResolution();
builder.AddIntegrationEvents();
if (singleRun is null)
{
    // The queue reader exists to drain a queue, and the per-Run mode has none: a pod habitat
    // dispatches through the outbox, and this process receives its one Run id as an argument.
    builder.AddRunDispatchReader();
}
builder.AddAgentRuntime();
builder.AddCodeWorkspace();
builder.AddLocalCodeWorkspace();

var modules = ModuleRegistration.Discover();
builder.Services.AddModules(modules, builder.Configuration);

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DispatchWorker");

if (singleRun is { } runId)
{
    WorkerLog.Claimed(logger, runId);

    await using (var scope = host.Services.CreateAsyncScope())
    {
        // Exit 0 means "execution happened" — the Run's own state carries success or failure
        // (#246 design D4). A failed Run is a completed execution; only a throw (no database,
        // no runtime) reaches the caller as non-zero, and BR-004 forbids retrying on it.
        var executor = scope.ServiceProvider.GetRequiredService<IRunExecutor>();
        await executor.Execute(runId);
    }

    return 0;
}

var reader = host.Services.GetRequiredService<DispatchQueueReader>();

// A "pass" drains the queue and returns. Draining rather than taking one message is
// deliberate: KEDA's scaler and the queue length are eventually consistent, so a job that
// handled exactly one message would leave stragglers waiting for the next poll.
// #144 design D2 — the only part of that change that prevents rather than recovers. A worker runs
// inside a platform budget (replica_timeout_in_seconds); when what is left of it is less than one
// full phase timeout, claiming another Run means starting work this process cannot finish. The
// sweeper (#140) would then close that Run as abandoned, which is recovery from a choice rather
// than from an accident.
//
// Leaving the message unclaimed is safe and is the point: the queue stays non-empty, KEDA sees it
// and starts a fresh job with a full budget.
var startedAt = DateTimeOffset.UtcNow;
var replicaBudget = TimeSpan.FromSeconds(
    builder.Configuration.GetValue("Dispatch:ReplicaBudgetSeconds", defaultValue: 3900)
);
var phaseCeiling = PhaseBudget.Maximum;

bool HasBudgetForAnotherPhase() =>
    replicaBudget - (DateTimeOffset.UtcNow - startedAt) >= phaseCeiling;

async Task<int> DrainOnce()
{
    var claimed = 0;

    while (HasBudgetForAnotherPhase() && await reader.Claim() is { } runId)
    {
        claimed++;
        WorkerLog.Claimed(logger, runId);

        // A scope per Run: the executor takes DbContexts, and one Run's failure must not leak
        // tracked state into the next.
        await using var scope = host.Services.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IRunExecutor>();
        await executor.Execute(runId);
    }

    if (!HasBudgetForAnotherPhase())
    {
        WorkerLog.BudgetExhausted(logger, phaseCeiling.TotalMinutes);
    }

    return claimed;
}

// Deployed, one pass IS the job: it drains and the process exits, which is what lets KEDA scale
// back to zero. Locally there is no KEDA, and an exited process picks up nothing — so the local
// composition sets this interval and a timer starts the next pass instead.
//
// The divergence is precisely one thing: WHAT decides to start a pass (a timer here, queue
// length in Azure). The pass itself is byte-for-byte the same code, so what the local loop
// proves about draining and executing is exactly what production does. What it proves about
// scaling is nothing.
var repeatInterval = builder.Configuration.GetValue<int?>("Dispatch:LocalPollSeconds");

if (repeatInterval is null or <= 0)
{
    WorkerLog.PassComplete(logger, await DrainOnce());
    return 0;
}

using var passes = new PeriodicTimer(TimeSpan.FromSeconds(repeatInterval.Value));

do
{
    var handled = await DrainOnce();

    if (handled > 0)
    {
        WorkerLog.PassComplete(logger, handled);
    }
} while (await passes.WaitForNextTickAsync());

return 0;

/// <summary>
/// Source-generated log delegates. Required by CA1848 rather than chosen — and the event ids are
/// unique across the solution so a log query can select one call site unambiguously.
/// </summary>
static partial class WorkerLog
{
    [LoggerMessage(EventId = 6001, Level = LogLevel.Information, Message = "Claimed run {RunId}")]
    public static partial void Claimed(ILogger logger, Guid runId);

    [LoggerMessage(
        EventId = 6004,
        Level = LogLevel.Information,
        Message = "Stopped claiming: less than one {PhaseCeilingMinutes} minute phase remains in this replica's budget. Unclaimed messages keep the queue non-empty, so KEDA starts a worker with a full budget"
    )]
    public static partial void BudgetExhausted(ILogger logger, double phaseCeilingMinutes);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Information,
        Message = "Dispatch pass complete: {Handled} claimed"
    )]
    public static partial void PassComplete(ILogger logger, int handled);
}
