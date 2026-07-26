using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.BuildingBlocks.IntegrationEvents;

/// <summary>
/// Reacts to an integration event. Delivery is at-least-once, so every implementation MUST be
/// idempotent: handling the same event twice must produce the same outcome as handling it once.
/// </summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : class, IIntegrationEvent
{
    Task Handle(TEvent @event, CancellationToken cancellationToken);
}

/// <summary>
/// One row of the wire-name → CLR-type index the relay dispatches by. Registered as data
/// (multiple singletons) because the index must exist before any container is built.
/// </summary>
public sealed record IntegrationEventRegistration(string EventName, Type EventType);

public static class IntegrationEventRegistrationExtensions
{
    /// <summary>
    /// Registers a handler and indexes its event's wire name. This is the only call a consuming
    /// module makes — no messaging type appears at the call site.
    /// </summary>
    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(
        this IServiceCollection services
    )
        where TEvent : class, IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        services.AddSingleton(new IntegrationEventRegistration(TEvent.EventName, typeof(TEvent)));
        return services;
    }
}
