namespace AiOrchestrator.Modules.Runs.Contracts;

/// <summary>
/// What other modules may ask about Runs. The Runs module was a leaf until this existed — it
/// consumed the Projects and Backlog contracts and nothing depended on it — so this is
/// deliberately the smallest possible surface: one question, asked by the one caller that has
/// it (deleting an Automation, #84).
/// </summary>
public interface IRunUsage
{
    /// <summary>
    /// How many Runs reference this Automation, in any state. Terminal Runs count: BR-014 keeps
    /// them for the audit trail, and that trail is exactly what deleting the Automation would
    /// break.
    /// </summary>
    Task<int> CountForAutomation(
        Guid projectId,
        Guid automationId,
        CancellationToken cancellationToken = default
    );
}
