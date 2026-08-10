using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Dispatch;
using DotNetCore.CAP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Dispatch;

/// <summary>
/// Dispatch without a queue (#225): the Run id goes through the same Postgres outbox integration
/// events use, and a subscriber in this process hands it to the executor.
/// <para>
/// The durability is the outbox, not the transport (design D3). CAP's in-memory transport would
/// lose a message with the process; its storage would not, so a Run accepted and lost to a
/// process death is redelivered by the fallback processor after restart. That is the whole reason
/// this is CAP rather than a <c>Channel&lt;Guid&gt;</c> — BR-004's crash story needs the message
/// to outlive the process, and a channel cannot promise that.
/// </para>
/// </summary>
public sealed class OutboxRunDispatcher(ICapPublisher cap) : IRunDispatcher
{
    public Task Dispatch(Guid runId, CancellationToken cancellationToken = default) =>
        // Published without a transaction, exactly as the queue dispatcher enqueues without one:
        // the Run is committed first and the message follows, so the visible-Queued-with-no-message
        // window this substrate has is the same window the queue path already documents.
        cap.PublishAsync(DispatchTopics.RunDispatched, runId, cancellationToken: cancellationToken);
}

/// <summary>
/// The consumer half, composed **only** by a host that should be able to execute Runs (design
/// D2). Registering <see cref="OutboxRunDispatcher"/> deliberately does not register this, so a
/// host that only publishes never acquires the ability to execute.
/// <para>
/// Since #246 the consumer claims and hands on: WHERE the Run executes is an
/// <see cref="IDispatchedRunHandler"/> the composition picked. The outbox stays the durable half
/// either way; the arrangement changes only the execution locus.
/// </para>
/// </summary>
public sealed class OutboxRunSubscriber(
    IDispatchedRunHandler handler,
    ILogger<OutboxRunSubscriber> logger
) : ICapSubscribe
{
    [CapSubscribe(DispatchTopics.RunDispatched)]
    public async Task Handle(Guid runId, CancellationToken cancellationToken)
    {
        DispatchLog.Claimed(logger, runId);
        await handler.Handle(runId, cancellationToken);
    }
}

/// <summary>
/// Today's arrangement, named: the Run executes in this process — which since #296 is the only
/// arrangement there is. Where the AGENT runs inside it is the launcher seam's question, not
/// this one.
/// </summary>
public sealed class InProcessRunHandler(IServiceProvider services) : IDispatchedRunHandler
{
    public async Task Handle(Guid runId, CancellationToken cancellationToken)
    {
        // A scope per Run, for the reason the worker's own loop gives: the executor takes
        // DbContexts, and one Run's failure must not leak tracked state into the next.
        await using var scope = services.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IRunExecutor>();

        // Nothing is guarded here on purpose. A redelivery — after a crash, or for a Run the
        // reaper has since terminated — is answered by the executor's own state check, which
        // logs and returns for anything not awaiting execution. One guard, on all substrates.
        await executor.Execute(runId, cancellationToken);
    }
}

/// <summary>The topic name, in one place so publisher and subscriber cannot drift.</summary>
static class DispatchTopics
{
    public const string RunDispatched = "aio.run.dispatched";
}

static partial class DispatchLog
{
    [LoggerMessage(
        EventId = 6210,
        Level = LogLevel.Information,
        Message = "Claimed run {RunId} from the outbox"
    )]
    public static partial void Claimed(ILogger logger, Guid runId);
}
