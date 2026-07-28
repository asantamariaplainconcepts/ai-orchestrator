namespace AiOrchestrator.BuildingBlocks.Agents;

/// <summary>
/// The job contract (DEC-012): an instruction goes in, a result comes out, and no vendor CLI
/// type crosses in either direction — the ISecretResolver placement rule. Implementations live
/// in host composition; modules and the worker see only this.
/// </summary>
public interface IAgentRuntime
{
    Task<AgentResult> Execute(AgentInstruction instruction, CancellationToken cancellationToken);
}

/// <summary>
/// Everything the runtime needs, in memory only (design D1): credentials were resolved by name
/// moments ago and exist nowhere at rest. The workspace is a directory the runtime may write.
/// </summary>
public sealed record AgentInstruction(
    string Prompt,
    string Action,
    TimeSpan Timeout,
    string WorkspacePath,
    AgentCredentials Credentials,
    /// <summary>
    /// Called once per output line while the agent works (#96). Optional so every existing
    /// caller is unchanged; runtimes that support it forward stdout as it arrives. Callbacks
    /// fire on process threads — implementations must be thread-safe and must not block.
    /// </summary>
    Action<string>? OnOutput = null
);

/// <summary>Values, never names — the inverse of everything stored (BR-010).</summary>
public sealed record AgentCredentials(string VendorAccessToken, string AiApiKey);

/// <summary>
/// What a run of the Agent came to. <see cref="Usage"/> is null when the runtime's output
/// carried none — BR-011 renders that as "unknown", never as a failure.
/// </summary>
public sealed record AgentResult(bool Succeeded, string Log, string? OutputLink, AgentUsage? Usage);

/// <summary>Tokens + cost at run end (BR-011, DEC-038).</summary>
public sealed record AgentUsage(long InputTokens, long OutputTokens, decimal CostUsd);

/// <summary>
/// The worker-facing surface of Run execution. The Runs module registers the implementation;
/// the worker host resolves it by interface — the same pattern as every other seam.
/// </summary>
public interface IRunExecutor
{
    /// <summary>
    /// Executes the claimed Run to a terminal state. A missing or non-Queued Run is a logged
    /// no-op — the message is already deleted (BR-004), nothing retries.
    /// </summary>
    Task Execute(Guid runId, CancellationToken cancellationToken = default);
}
