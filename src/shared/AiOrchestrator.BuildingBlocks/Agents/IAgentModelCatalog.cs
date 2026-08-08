namespace AiOrchestrator.BuildingBlocks.Agents;

/// <summary>
/// What models a runtime offers, for the surfaces that let somebody choose one (#291). A seam for
/// the same reason <see cref="IAgentRuntimesMonitor"/> is one: the modules that render the chooser
/// must never spawn a CLI, and the habitat that executes keeps the answer true.
/// </summary>
public interface IAgentModelCatalog
{
    Task<AgentModelOptions> For(string runtimeName, CancellationToken cancellationToken = default);
}

/// <summary>
/// One runtime's offer, in the three states a chooser has to tell apart (design D6). They are not
/// collapsible: an empty list and a machine that could not be asked send a reader to different
/// places, and rendering the second as the first says a runtime has no models when nobody looked.
/// </summary>
/// <param name="Models">What to offer. Empty is only meaningful with <see cref="AgentModelSource.Declared"/>.</param>
/// <param name="Source">Where the offer came from, so the surface can say so.</param>
public sealed record AgentModelOptions(IReadOnlyList<string> Models, AgentModelSource Source)
{
    /// <summary>The machine could not be asked — not an answer about the runtime's models.</summary>
    public static AgentModelOptions Unasked { get; } = new([], AgentModelSource.CouldNotAsk);

    /// <summary>Nothing is known about this runtime at all — it is not registered here.</summary>
    public static AgentModelOptions None { get; } = new([], AgentModelSource.Declared);
}

/// <summary>
/// The default every habitat starts from, so the chooser's endpoint always resolves — the same
/// discipline the pods and runtimes monitors already follow: the ability is absent, never the
/// answer. A process that executes no Runs has no machine to ask and no operator list to read,
/// so it declares nothing rather than reporting a failure to reach something that was never there.
/// </summary>
public sealed class NoAgentModelCatalog : IAgentModelCatalog
{
    public Task<AgentModelOptions> For(
        string runtimeName,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(AgentModelOptions.None);
}

public enum AgentModelSource
{
    /// <summary>
    /// The runtime listed them itself, on the machine that will run it. The only source that
    /// cannot be stale relative to what a Run would actually reach.
    /// </summary>
    Enumerated = 1,

    /// <summary>
    /// An operator declared them, because this runtime has no listing command. Empty here means
    /// nobody has declared any — a real answer, and a different one from the state below.
    /// </summary>
    Declared = 2,

    /// <summary>
    /// The machine that would answer could not be reached. Says nothing about the models, and
    /// must never be rendered as though it did.
    /// </summary>
    CouldNotAsk = 3,
}
