using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AiOrchestrator.BuildingBlocks.IntegrationEvents;

/// <summary>
/// Publishes integration events. Product vocabulary only — no messaging type appears here, so a
/// module can announce facts without referencing infrastructure (the ISecretResolver rule).
/// </summary>
public interface IIntegrationEventPublisher
{
    /// <summary>
    /// Begins a transaction in which the module's own writes and its published events commit or
    /// roll back <b>together</b> — the property that makes events trustworthy. The module calls
    /// <c>SaveChanges</c> and <see cref="Publish{TEvent}"/> inside it, then commits the handle.
    /// </summary>
    Task<IIntegrationEventTransaction> BeginTransaction(
        DatabaseFacade database,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Announces an event. Inside a transaction from <see cref="BeginTransaction"/> it is staged
    /// with the transaction; outside one it is published immediately. Delivery is at-least-once:
    /// every handler must tolerate duplicates.
    /// </summary>
    Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;
}

/// <summary>Commit handle. Disposing without committing rolls back — writes and events alike.</summary>
public interface IIntegrationEventTransaction : IAsyncDisposable
{
    Task Commit(CancellationToken cancellationToken = default);
}
