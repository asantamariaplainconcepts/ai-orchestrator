using AiOrchestrator.Modules.Projects.Contracts;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.Modules.Runs.Features.Execution;

/// <summary>
/// #140 — BR-005 made true when the process enforcing it does not survive.
/// <para>
/// The phase timeout is a <c>CancelAfter</c> inside the agent's process, so it cannot fire when
/// that process is gone. A container recycled, an out-of-memory kill or a job eviction raises no
/// exception, so the executor's own <c>catch</c> never runs, and nothing else looks at the state —
/// leaving the Run in <c>Executing</c> for ever and its Story blocked by BR-001.
/// </para>
/// <para>
/// Deliberately <b>not</b> a heartbeat (design D3). A Run past its deadline is over by the contract
/// already written: were its process alive it would have cancelled itself, so exceeding the
/// deadline is itself the evidence that it is not. A heartbeat would add a second definition of
/// "still running", a cadence to tune, and a way to kill a briefly starved worker for reporting
/// late.
/// </para>
/// </summary>
sealed partial class AbandonedRunReaper(
    IServiceScopeFactory scopeFactory,
    RunsOptions options,
    ILogger<AbandonedRunReaper> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.ReapInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<RunReaping>().Sweep(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // One bad pass must never take the sweep down: the Runs it would have ended are
                // still overdue on the next tick, because overdue-ness is a property of the Run.
                LogSweepFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(
        EventId = 8002,
        Level = LogLevel.Error,
        Message = "A reaper pass failed; overdue Runs remain overdue and the next tick retries"
    )]
    static partial void LogSweepFailed(ILogger logger, Exception exception);
}

/// <summary>
/// The sweep itself, separated from the timer that drives it — the shape the backlog poller and
/// its synchroniser already use, and for the same reason: a test drives this deterministically
/// instead of racing a <see cref="PeriodicTimer"/>.
/// </summary>
sealed partial class RunReaping(
    RunsDbContext database,
    IAutomationCatalog automations,
    TimeProvider clock,
    RunsOptions options,
    ILogger<RunReaping> logger
)
{
    public async Task Sweep(CancellationToken cancellationToken)
    {
        // Read the candidates without their deadlines: the timeout lives in the Projects module,
        // so it is asked for per candidate rather than joined across a module boundary.
        var candidates = await database
            .Runs.Where(run =>
                (run.State == RunState.Planning || run.State == RunState.Executing)
                && run.StartedAt != null
            )
            .ToListAsync(cancellationToken);

        foreach (var run in candidates)
        {
            var automation = await automations.Detail(
                run.ProjectId,
                run.AutomationId,
                cancellationToken
            );

            // No Automation means it was deleted under a running Run. The framework's default is
            // the honest fallback: refusing to reap would leave exactly the eternal Executing
            // this exists to end.
            var timeout = automation?.Timeout ?? DefaultTimeout;
            var deadline = PhaseStart(run) + timeout + options.ReapGrace;

            if (clock.GetUtcNow() < deadline)
            {
                continue;
            }

            // Distinct from the executor's own timeout message (design D2): an agent that ran out
            // of time asks for a bigger budget, a worker that vanished asks somebody to look at
            // the infrastructure.
            run.Fail(
                clock.GetUtcNow(),
                $"The Run passed its {timeout.TotalMinutes:0} minute timeout without its worker "
                    + "reporting. Nothing was retried; re-trigger it when the cause is understood."
            );

            // Conditional on the state we observed, which is the guarantee rather than the guess
            // (design D5): a Run that reached a terminal state between the read and this write
            // keeps its own outcome, and the reaper's change is discarded.
            var written = await database
                .Runs.Where(entity =>
                    entity.Id == run.Id
                    && (entity.State == RunState.Planning || entity.State == RunState.Executing)
                )
                .ExecuteUpdateAsync(
                    setters =>
                        setters
                            .SetProperty(entity => entity.State, RunState.Failed)
                            .SetProperty(entity => entity.EndedAt, run.EndedAt)
                            .SetProperty(entity => entity.FailureReason, run.FailureReason),
                    cancellationToken
                );

            if (written > 0)
            {
                LogReaped(logger, run.Id, timeout.TotalMinutes);
            }
        }
    }

    /// <summary>
    /// When the phase this Run is currently in began (#146). BR-005 gives each <i>phase</i> a
    /// timeout and BR-006 says human waits are untimed, so measuring from <c>StartedAt</c> — which
    /// <c>MarkPlanning</c> sets, before the wait — timed the approval itself. A Run that planned on
    /// Monday and was approved on Tuesday was past its deadline the instant it began executing.
    /// </summary>
    static DateTimeOffset PhaseStart(Run run) =>
        run.State == RunState.Executing && run.ApprovedAt is { } approved
            ? approved
            : run.StartedAt!.Value;

    /// <summary>Only reachable when an Automation was deleted mid-Run; matches the seeded default.</summary>
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);

    [LoggerMessage(
        EventId = 8001,
        Level = LogLevel.Warning,
        Message = "Run {RunId} passed its {TimeoutMinutes} minute timeout with no worker reporting and was failed — its Story is free again"
    )]
    static partial void LogReaped(ILogger logger, Guid runId, double timeoutMinutes);
}
