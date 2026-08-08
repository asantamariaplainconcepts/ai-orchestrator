using AiOrchestrator.BuildingBlocks.Agents;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// Answers what models a runtime offers, by the mechanism that runtime declares (#291, design D1):
/// it is asked where it can be asked, and read from the operator's configuration where it cannot.
/// <para>
/// The branch is on <see cref="AgentRuntimeSelection.ModelListArguments"/> rather than on a
/// runtime's name, so adding a runtime stays a composition change — the rule the selector seam
/// already enforces for everything else it carries.
/// </para>
/// </summary>
public sealed class AgentModelCatalog(IAgentRuntimeSelector runtimes, IAgentProcessHost processHost)
    : IAgentModelCatalog
{
    public async Task<AgentModelOptions> For(
        string runtimeName,
        CancellationToken cancellationToken = default
    )
    {
        if (runtimes.For(runtimeName) is not { } selection)
        {
            // Not registered here. "Nothing declared" is the honest shape: there is no machine to
            // have failed to ask, so reporting a failure to ask would invent one.
            return AgentModelOptions.None;
        }

        if (selection.ModelListArguments is not { } arguments)
        {
            return new AgentModelOptions(selection.ConfiguredModels, AgentModelSource.Declared);
        }

        var listed = await processHost.ListModels(selection.Command, arguments, cancellationToken);

        // Null is "could not ask", and it stays that all the way out (design D6). Note what is
        // NOT done here: falling back to the configured list would quietly answer a different
        // question, and the reader would never learn the machine was unreachable.
        return listed is null
            ? AgentModelOptions.Unasked
            : new AgentModelOptions(listed, AgentModelSource.Enumerated);
    }
}
