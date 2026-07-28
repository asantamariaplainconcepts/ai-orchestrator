using AiOrchestrator.Modules.Runs.Contracts;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Features.Observation;

/// <summary>The Contracts read surface for Run usage, implemented by the module that owns Runs.</summary>
sealed class RunUsage(RunsDbContext database) : IRunUsage
{
    public Task<int> CountForAutomation(
        Guid projectId,
        Guid automationId,
        CancellationToken cancellationToken = default
    ) =>
        database.Runs.CountAsync(
            run => run.ProjectId == projectId && run.AutomationId == automationId,
            cancellationToken
        );
}
