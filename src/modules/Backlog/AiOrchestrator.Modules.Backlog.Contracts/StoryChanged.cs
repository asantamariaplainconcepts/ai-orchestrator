using AiOrchestrator.BuildingBlocks.IntegrationEvents;

namespace AiOrchestrator.Modules.Backlog.Contracts;

/// <summary>
/// The "normalized story event" UC-009 names: the Mirror recorded that a Story appeared,
/// changed, or vanished. Identity and change-kind only — a consumer wanting the Story's current
/// labels or state reads them through a contract, because carried state is stale state.
/// <para>
/// This is the first type in the first Contracts assembly — the pattern the guardrails ratified
/// at bootstrap. Public on purpose: contracts are the one place a module's types cross the
/// boundary.
/// </para>
/// </summary>
public sealed record StoryChanged(Guid ProjectId, string VendorStoryId, StoryChangeKind Kind)
    : IIntegrationEvent
{
    /// <summary>Versioned wire name; an incompatible shape change means a new name (design D2).</summary>
    public static string EventName => "backlog.story-changed.v1";
}

public enum StoryChangeKind
{
    Added = 1,
    Updated = 2,
    Removed = 3,
}
