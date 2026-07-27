using AiOrchestrator.Modules.Backlog.Contracts;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog;

/// <summary>
/// The repository-level write surface, implemented by the owner of the Connector.
/// <para>
/// Every outcome is a sentence rather than a throw, including "there is no Connector". A caller
/// applying default Automations needs to finish and then tell the Admin what happened; a project
/// with nothing connected is an ordinary state, not an exception.
/// </para>
/// </summary>
sealed class LabelWriter(ConnectorAccess access) : ILabelWriter
{
    public async Task<string?> EnsureLabels(
        Guid projectId,
        IReadOnlyList<string> labels,
        CancellationToken cancellationToken = default
    )
    {
        if (labels.Count == 0)
        {
            return null;
        }

        var context = await access.Resolve(projectId, cancellationToken);
        if (context.IsError)
        {
            return context.FirstError.Description;
        }

        var (connector, coordinates, token) = context.Value;

        // Every label is attempted even after one fails, and the failures are reported together.
        // Stopping at the first would leave the Admin fixing them one press at a time, learning
        // about each only after solving the last.
        var failed = new List<string>();

        foreach (var label in labels)
        {
            var result = await connector.EnsureLabel(coordinates, label, token, cancellationToken);
            if (result.IsError)
            {
                failed.Add($"{label} ({result.FirstError.Description})");
            }
        }

        return failed.Count == 0 ? null : $"Could not ensure: {string.Join("; ", failed)}.";
    }
}
