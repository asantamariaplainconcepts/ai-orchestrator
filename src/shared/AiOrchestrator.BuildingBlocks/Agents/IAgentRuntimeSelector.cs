namespace AiOrchestrator.BuildingBlocks.Agents;

/// <summary>
/// Maps an Automation's runtime name to its implementation and its credential's <b>name</b>
/// (opencode-runtime design D1). Selection is composition: a new runtime is a registration,
/// never an executor edit. A null credential name means the runtime needs none resolved —
/// free providers are a supported configuration, not an error path (design D3).
/// </summary>
public interface IAgentRuntimeSelector
{
    /// <summary>The runtime for the name, or null when no such runtime is registered.</summary>
    AgentRuntimeSelection? For(string runtimeName);

    /// <summary>
    /// Every registered runtime, for the readiness probe (#279): observability enumerates what
    /// selection only looks up.
    /// </summary>
    IReadOnlyDictionary<string, AgentRuntimeSelection> Registered { get; }
}

/// <summary>
/// One registered runtime: the implementation, the credential's name (null when none is
/// required — the switched-off state runs with the machine's own session, #279), the executable
/// the runtime spawns, and the copyable install command for the machine that lacks it. The
/// command and its remedy live on the selection so the probe, the panel and a Run's failure all
/// read the same fact (design D3).
/// </summary>
public sealed record AgentRuntimeSelection(
    IAgentRuntime Runtime,
    string? CredentialSecretName,
    string Command,
    string InstallCommand
)
{
    /// <summary>
    /// How the agent will authenticate, in words a transcript can carry: the values this Run
    /// resolved, or a sandbox host that authenticates on the agent's behalf so no value enters
    /// its reach. Composition knows which, because composition chose the host; the executor
    /// writes it beside its other credential sentences, and a reader never has to infer whether
    /// a secret entered the agent's reach.
    /// </summary>
    public string CredentialSource { get; init; } = "carried in the agent process's environment";

    /// <summary>
    /// How this runtime is asked to list its models, or null when it cannot be asked (#291,
    /// design D1). `opencode models` answers; Claude Code has no such command, so it is null and
    /// <see cref="ConfiguredModels"/> is what it offers instead.
    /// <para>
    /// Declared on the selection rather than branched on the runtime's name, so adding a runtime
    /// stays a composition change (the rule this seam already enforces for everything else).
    /// </para>
    /// </summary>
    public IReadOnlyList<string>? ModelListArguments { get; init; }

    /// <summary>
    /// The models an operator declared for this runtime, used where it cannot be asked. Empty is
    /// meaningful: it says nobody has declared any, which the chooser renders differently from
    /// having failed to ask (design D6).
    /// <para>
    /// It is configuration and not code on purpose. #291 measured Claude Code 2.0.44 rejecting
    /// two of three plausible aliases — `opus` resolves to a model this seat does not have and
    /// `fable` is not an alias at all — so a list compiled into the product would ship broken.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> ConfiguredModels { get; init; } = [];
}
