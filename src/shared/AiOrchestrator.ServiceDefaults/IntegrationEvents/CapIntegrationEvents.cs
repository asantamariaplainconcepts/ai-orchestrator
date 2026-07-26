using System.Text.Json;
using AiOrchestrator.BuildingBlocks.IntegrationEvents;
using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.IntegrationEvents;

/// <summary>
/// The CAP side of the integration-event seam. Everything CAP lives in this folder; modules see
/// only the BuildingBlocks interfaces (design D1 — wrapped, not sanctioned).
/// <para>
/// One topic carries every event. CAP subscribes methods to static topic names, and a relay per
/// event type would put CAP attributes where modules can see them; a single envelope topic keeps
/// the fan-out in one place and dispatches by the versioned wire name instead.
/// </para>
/// </summary>
public static class CapTopics
{
    public const string IntegrationEvents = "integration-events";
}

/// <summary>The wire shape: versioned name + payload. The name IS the version (design D2).</summary>
public sealed record EventEnvelope(string Name, string Payload);

public sealed class CapIntegrationEventPublisher(ICapPublisher cap) : IIntegrationEventPublisher
{
    static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    public Task<IIntegrationEventTransaction> BeginTransaction(
        DatabaseFacade database,
        CancellationToken cancellationToken = default
    )
    {
        // CAP's EF extension: the returned transaction spans the module's writes AND the staged
        // event rows, which is the entire point of using an outbox (design D3).
        var transaction = database.BeginTransaction(cap, autoCommit: false);
        return Task.FromResult<IIntegrationEventTransaction>(new CapTransaction(transaction));
    }

    public Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        var envelope = new EventEnvelope(TEvent.EventName, JsonSerializer.Serialize(@event, Wire));

        return cap.PublishAsync(
            CapTopics.IntegrationEvents,
            envelope,
            cancellationToken: cancellationToken
        );
    }

    sealed class CapTransaction(IDbContextTransaction transaction) : IIntegrationEventTransaction
    {
        public Task Commit(CancellationToken cancellationToken = default) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}

/// <summary>
/// The one CAP subscriber. Receives every envelope, resolves the event type from the registered
/// index, and fans out to the module handlers — which never know CAP exists.
/// </summary>
public sealed class CapIntegrationEventRelay(
    IServiceProvider services,
    IEnumerable<IntegrationEventRegistration> registrations,
    ILogger<CapIntegrationEventRelay> logger
) : ICapSubscribe
{
    static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    readonly Dictionary<string, Type> _index = registrations
        .GroupBy(registration => registration.EventName)
        .ToDictionary(group => group.Key, group => group.First().EventType, StringComparer.Ordinal);

    [CapSubscribe(CapTopics.IntegrationEvents)]
    public async Task Handle(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        // An unknown name is a shape this process does not understand — a future version, or an
        // event with no registered consumer. Dropped explicitly (the spec's refusal scenario):
        // nothing will ever make it parse, and CAP's retry would just replay the confusion.
        if (!_index.TryGetValue(envelope.Name, out var eventType))
        {
            RelayLog.UnknownEvent(logger, envelope.Name);
            return;
        }

        var @event = JsonSerializer.Deserialize(envelope.Payload, eventType, Wire);
        if (@event is null)
        {
            RelayLog.UnreadablePayload(logger, envelope.Name);
            return;
        }

        // Handlers are scoped (they take DbContexts); each delivery gets a fresh scope.
        await using var scope = services.CreateAsyncScope();
        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
        var handleMethod = handlerType.GetMethod(nameof(IIntegrationEventHandler<>.Handle))!;

        foreach (var handler in scope.ServiceProvider.GetServices(handlerType))
        {
            // A throwing handler propagates: CAP records the failure and retries up to the
            // deliberate ceiling. Swallowing here would turn every defect into silence.
            await (Task)handleMethod.Invoke(handler, [@event, cancellationToken])!;
        }
    }
}

static partial class RelayLog
{
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Warning,
        Message = "Dropped integration event with unknown name {EventName} — no registered consumer or a version this build predates"
    )]
    public static partial void UnknownEvent(ILogger logger, string eventName);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Error,
        Message = "Dropped integration event {EventName}: payload did not deserialise"
    )]
    public static partial void UnreadablePayload(ILogger logger, string eventName);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Error,
        Message = "Integration event {EventName} exhausted its retries and is dead — it will never be redelivered automatically; someone must look"
    )]
    public static partial void DeadLettered(ILogger logger, string eventName);
}
