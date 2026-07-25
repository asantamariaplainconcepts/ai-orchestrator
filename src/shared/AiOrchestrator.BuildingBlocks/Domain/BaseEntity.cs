namespace AiOrchestrator.BuildingBlocks.Domain;

/// <summary>
/// Root of the domain model. Identifiers are GUID v7 — time-ordered, so they index well as
/// primary keys. Entities are <c>internal</c> to their module (enforced by MOD003).
/// </summary>
public abstract class BaseEntity(Guid id)
{
    protected BaseEntity()
        : this(Guid.CreateVersion7()) { }

    public Guid Id { get; private init; } = id;
}

/// <summary>A consistency boundary: the only entity type a repository loads or saves directly.</summary>
public abstract class Aggregate(Guid id) : BaseEntity(id)
{
    protected Aggregate()
        : this(Guid.CreateVersion7()) { }
}
