using AiOrchestrator.BuildingBlocks.Domain;

namespace AiOrchestrator.Modules.Projects.Domain;

/// <summary>
/// A unit of configuration: one Connector, its Automations, its caps (BC-001).
/// Only the name exists at this stage — Connector and Automation arrive as product changes.
/// </summary>
sealed class Project : Aggregate
{
    Project() { }

    Project(string name) => Name = name;

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// When this Project was retired, or null while it is live (#121, design D3). A timestamp
    /// rather than a flag because the list wants to say <i>when</i>, and a boolean would need a
    /// second column the moment anybody asked.
    /// <para>
    /// Archiving stops new work — no polling, no matching, no manual Run — and stops nothing
    /// else: what its agents already did stays readable, because BR-014 makes that record the
    /// audit trail rather than clutter.
    /// </para>
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    public static Project Create(string name) => new(name);

    /// <summary>Idempotent: archiving an archived Project keeps the original moment.</summary>
    public void Archive(DateTimeOffset at) => ArchivedAt ??= at;

    public void Restore() => ArchivedAt = null;
}
