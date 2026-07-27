using AiOrchestrator.BuildingBlocks.Dispatch;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.Modules.Runs.Features.Conversation;

/// <summary>
/// Wakes waiting Runs whose humans have answered. Comments are never mirrored (BR-008), so no
/// event announces an answer — this polls instead, and only over Runs that are actually waiting,
/// which is a handful of one-page reads (design D3).
/// <para>
/// Opt-in like the backlog poller: the host enables it, the functional test host drives
/// <see cref="CheckOnce"/> deterministically instead.
/// </para>
/// </summary>
sealed partial class ResumeChecker(
    IServiceScopeFactory scopeFactory,
    TimeSpan interval,
    ILogger<ResumeChecker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await CheckOnce(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // One bad pass must never take the checker down; the next tick tries again.
                LogPassFailed(logger, exception);
            }
        }
    }

    /// <summary>One pass, callable deterministically from tests.</summary>
    public static async Task CheckOnce(
        IServiceProvider services,
        CancellationToken cancellationToken
    )
    {
        var database = services.GetRequiredService<RunsDbContext>();
        var gate = services.GetRequiredService<ConversationGate>();
        var dispatcher = services.GetRequiredService<IRunDispatcher>();
        var logger = services.GetRequiredService<ILogger<ResumeChecker>>();

        var waiting = await database
            .Runs.Where(run => run.State == RunState.AwaitingInput)
            .ToListAsync(cancellationToken);

        foreach (var run in waiting)
        {
            var answers = await gate.AnswersFor(run, cancellationToken);

            if (answers.Failure is not null)
            {
                // The vendor being unreachable is the next tick's problem, not this Run's
                // failure — waiting is exactly the state that can afford to wait.
                LogReadFailed(logger, run.Id, answers.Failure);
                continue;
            }

            if (answers.Comments.Count == 0)
            {
                continue;
            }

            run.Resume();
            await database.SaveChangesAsync(cancellationToken);
            await dispatcher.Dispatch(run.Id, cancellationToken);
            LogResumed(logger, run.Id, answers.Comments.Count);
        }
    }

    [LoggerMessage(
        EventId = 7101,
        Level = LogLevel.Information,
        Message = "Run {RunId} resumed — {Answers} answer(s) arrived"
    )]
    static partial void LogResumed(ILogger logger, Guid runId, int answers);

    [LoggerMessage(
        EventId = 7102,
        Level = LogLevel.Warning,
        Message = "Run {RunId} stays waiting — could not read its conversation: {Reason}"
    )]
    static partial void LogReadFailed(ILogger logger, Guid runId, string reason);

    [LoggerMessage(
        EventId = 7103,
        Level = LogLevel.Error,
        Message = "A resume pass failed; the next tick will retry"
    )]
    static partial void LogPassFailed(ILogger logger, Exception exception);
}
