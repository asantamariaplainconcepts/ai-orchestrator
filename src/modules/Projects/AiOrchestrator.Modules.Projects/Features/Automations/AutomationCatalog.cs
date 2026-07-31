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

    public async Task<AutomationDetail?> Detail(
        Guid projectId,
        Guid automationId,
        CancellationToken cancellationToken = default
    )
    {
        // Materialise then project: the detail carries enum names, and ToString() inside an EF
        // projection translates to SQL rather than evaluating in .NET (the #7 lesson).
        // No Enabled filter: an in-flight Run must be able to finish under an Automation that
        // was disabled after it started (UC-006 — disabling stops future matches only).
        var automation = await database.Automations.FirstOrDefaultAsync(
            entity => entity.Id == automationId && entity.ProjectId == projectId,
            cancellationToken
        );

        return automation is null
            ? null
            : new AutomationDetail(
                automation.Id,
                automation.TriggerLabel,
                automation.Action.ToString(),
                automation.Runtime.ToString(),
                automation.RequiresApproval,
                automation.Timeout,
                automation.PromptPath,
                automation.OutputLabels
            );
    }
}
