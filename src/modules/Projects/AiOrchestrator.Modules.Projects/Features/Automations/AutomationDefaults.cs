using AiOrchestrator.Modules.Projects.Domain;

namespace AiOrchestrator.Modules.Projects.Features.Automations;

/// <summary>
/// How this framework is meant to be used, expressed as configuration a project can start with.
/// <para>
/// In code rather than in data (design D1). Which Automations a project begins with is a claim
/// about the product, and it should arrive as a reviewed commit with its reasoning attached —
/// not as a row somebody edited once, in an environment nobody can now remember.
/// </para>
/// </summary>
static class AutomationDefaults
{
    /// <summary>
    /// The free model (DEC-044). A single click that quietly starts spending on a paid runtime
    /// is a bad default, and the runtime is one dropdown away for anyone who wants otherwise.
    /// </summary>
    const AgentRuntime Runtime = AgentRuntime.OpenCode;

    public static readonly IReadOnlyList<AutomationDefault> All =
    [
        // Ordered as the workflow reads. Grill decides readiness; propose listens on the label
        // grill produces, which is what makes the seeded set a pipeline rather than six
        // unrelated triggers (design D1). Nothing overrides the grill's documented ready label —
        // propose simply hears it.
        new("ai:grill", AutomationAction.GrillToReady, RequiresApproval: false),
        new("ready-for-proposal", AutomationAction.ProposeSpec, RequiresApproval: false),
        // The chain stops here on purpose (D2): propose applies no label, so a human reads the
        // proposal and labels this one when convinced. It is also the only action that writes
        // code and opens a pull request, so it is the only default that waits for a human
        // (DEC-040) — the rest write comments, labels and documentation.
        new("ai:implement", AutomationAction.ImplementToPullRequest, RequiresApproval: true),
        new("ai:refine", AutomationAction.RefineOrComment, RequiresApproval: false),
        new("ai:estimate", AutomationAction.Estimate, RequiresApproval: false),
        new("ai:transition", AutomationAction.TransitionState, RequiresApproval: false),
    ];

    public static IReadOnlyList<string> Labels => [.. All.Select(entry => entry.TriggerLabel)];

    public static Automation ToAutomation(this AutomationDefault entry, Guid projectId) =>
        Automation.Create(
            projectId,
            entry.TriggerLabel,
            // No state condition: a default that only fired on one vendor's state vocabulary
            // would be wrong for the other (DEC-045).
            triggerState: null,
            entry.Action,
            Runtime,
            entry.RequiresApproval,
            UseCases.CreateAutomation.DefaultTimeout
        );
}

sealed record AutomationDefault(
    string TriggerLabel,
    AutomationAction Action,
    bool RequiresApproval
);
