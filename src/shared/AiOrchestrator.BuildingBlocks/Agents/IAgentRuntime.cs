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
    Action<string>? OnOutput = null,
    /// <summary>
    /// What to publish while the agent works, so a Member can look at the change running
    /// (run-previews). Null means no preview — every Run until an Automation names a port, and
    /// every Run in a habitat whose host cannot publish, because only a sandbox has a port.
    /// </summary>
    RunPreview? Preview = null,
    /// <summary>
    /// The model to think with, resolved by the executor (#291). Null means the runtime launches
    /// exactly as it did before this existed — the CLI's own default, or the deployment's — which
    /// is what every Run does in a deployment that has chosen nothing.
    /// <para>
    /// Carried here rather than read from options by each runtime, because the value is a
    /// property of the Run being executed, not of the process executing it.
    /// </para>
    /// </summary>
    string? Model = null,
    /// <summary>
    /// The Project this Run belongs to (#296). Carried because a host may scope isolation by
    /// Project — the Azure launcher creates each Project's sandboxes in that Project's own group,
    /// so a Run bills and acts as its own Project (#244). Hosts that do not scope by Project
    /// ignore it, which is every other one.
    /// </summary>
    Guid? ProjectId = null,
    /// <summary>
    /// Which Run this is (#304). Carried so a host that creates a sandbox can publish which sandbox
    /// belongs to which Run, which is what lets a human open a terminal in it.
    /// <para>
    /// <see cref="Preview"/> already carries a Run id, and deliberately is not used for this: a
    /// preview exists only when an Automation named a port, so reading the id from it would make the
    /// terminal available exactly when a preview happened to be too. Null means the caller did not
    /// say, and a host that cannot name the Run simply publishes nothing.
    /// </para>
    /// </summary>
    Guid? RunId = null
);

/// <summary>
/// A preview to publish for the life of one agent: which port inside the sandbox serves it, and
/// which Run it belongs to. The two travel together because the ledger is keyed by the Run and
/// the publish is keyed by the port, and separating them invites recording one without the other.
/// </summary>
public sealed record RunPreview(Guid RunId, int SandboxPort);

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
