namespace AiOrchestrator.BuildingBlocks.IntegrationEvents;

/// <summary>
/// A fact one module announces so others can react — identity and change-kind, never entity
/// state (consumers read current truth through Contracts; carrying state invites staleness,
/// the same reasoning as the dispatch message).
/// <para>
/// The name carries the version (<c>backlog.story-changed.v1</c>): a consumer that does not
/// recognise a name drops the event explicitly rather than misreading a shape it predates.
/// </para>
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>Versioned wire name. Changing an event's shape incompatibly means a new name.</summary>
    static abstract string EventName { get; }
}
