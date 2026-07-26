using AiOrchestrator.Modules.Projects.Contracts;
using AiOrchestrator.Modules.Projects.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Projects.Features.Automations;

/// <summary>
/// The Contracts read surface, implemented by the owner. Only enabled Automations are visible —
/// BR-003 and matching both speak about enabled ones, so a disabled trigger simply does not
/// exist to a consumer.
/// </summary>
sealed class AutomationCatalog(ProjectsDbContext database) : IAutomationCatalog
{
    public async Task<IReadOnlyList<AutomationTrigger>> EnabledAutomations(
        Guid projectId,
        CancellationToken cancellationToken = default
    ) =>
        await database
            .Automations.Where(automation =>
                automation.ProjectId == projectId && automation.Enabled
            )
            .Select(automation => new AutomationTrigger(
                automation.Id,
                automation.TriggerLabel,
                automation.TriggerState,
                automation.RequiresApproval
            ))
            .ToListAsync(cancellationToken);
}
