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
}

public sealed record AgentRuntimeSelection(IAgentRuntime Runtime, string? CredentialSecretName);
