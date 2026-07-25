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

    public static Project Create(string name) => new(name);
}
