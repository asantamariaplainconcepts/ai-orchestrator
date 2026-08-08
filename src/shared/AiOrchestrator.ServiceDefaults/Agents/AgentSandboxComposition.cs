using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// Where a habitat's agents execute (design D1, D5). Presence of configuration decides, never an
/// environment name (ADR-0010): a launcher named selects the sandboxed process host; nothing
/// named keeps the agent a child of this process, exactly as before sandboxing existed.
/// </summary>
public static class AgentSandboxComposition
{
    /// <summary>
    /// Naming the launcher is what opts a habitat into sandboxing. The only supported value is
    /// <c>sbx</c> — a closed set rather than free text, so a typo refuses instead of silently
    /// running the agent unsandboxed in a habitat that asked for isolation.
    /// </summary>
    public const string LauncherKey = "Agents:Sandbox:Launcher";

    /// <summary>The sbx binary, for a machine where it is not on PATH (the spike's own case).</summary>
    public const string CommandPathKey = "Agents:Sandbox:CommandPath";

    /// <summary>
    /// Explicit rather than sbx's default of 50% of host RAM (spike H5): two concurrent
    /// sandboxes at that default exhaust the machine.
    /// </summary>
    public const string MemoryKey = "Agents:Sandbox:Memory";

    /// <summary>
    /// Which credential services the launcher injects at egress. Named, because a launcher that
    /// claims injection and holds nothing would start an unauthenticated agent (design D2).
    /// </summary>
    public const string InjectedSecretsKey = "Agents:Sandbox:InjectedSecrets";

    /// <summary>
    /// Which runtime commands sbx has an agent template for — the sandbox images that actually
    /// carry the CLI. A new runtime is a configuration line, never an executor edit.
    /// </summary>
    public const string AgentTemplatesKey = "Agents:Sandbox:AgentTemplates";

    /// <summary>
    /// Whether the machine owner's agent-CLI credentials are copied into each sandbox (#288).
    /// Off unless a habitat declares it, and only the dev loop does: a carried session is
    /// readable by whatever runs in the sandbox, which is acceptable where a developer runs
    /// their own repositories and is not where a habitat runs somebody else's. A Run under
    /// carriage acts and bills as that seat.
    /// </summary>
    public const string CarrySessionKey = "Agents:Sandbox:CarrySession";

    /// <summary>Which credential files travel; the default is the observed set.</summary>
    public const string SessionFilesKey = "Agents:Sandbox:SessionFiles";

    public const string SbxLauncher = "sbx";

    internal static void AddAgentProcessHost(IHostApplicationBuilder builder)
    {
        var launcher = builder.Configuration.GetValue<string?>(LauncherKey);

        if (string.IsNullOrWhiteSpace(launcher))
        {
            builder.Services.AddSingleton<IAgentProcessHost, LocalAgentProcessHost>();
            return;
        }

        if (!string.Equals(launcher, SbxLauncher, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"'{LauncherKey}' is set to '{launcher}', which is not a launcher this build "
                    + $"knows. The supported value is '{SbxLauncher}'. Remove the key to run the "
                    + "agent as a child of this process."
            );
        }

        // Two isolation substrates at once is a question, not a configuration (design D5):
        // the pod already isolates the whole worker, and a sandbox inside it would make one of
        // the operator's two choices an invisible no-op. Refusing names both, exactly as a host
        // holding both sides of the queue boundary is refused.
        var podImage = builder.Configuration.GetValue<string?>("Dispatch:PodImage");
        if (!string.IsNullOrWhiteSpace(podImage))
        {
            throw new InvalidOperationException(
                $"This habitat names both a pod image (Dispatch:PodImage = {podImage}) and an "
                    + $"agent sandbox ({LauncherKey} = {launcher}). Each isolates the agent on its "
                    + "own terms, and running both would nest one inside the other while making "
                    + "one of the two invisible. Remove whichever is not intended."
            );
        }

        builder.Services.AddSingleton(
            new SbxSandboxOptions
            {
                CommandPath =
                    builder.Configuration.GetValue<string?>(CommandPathKey)
                    ?? SbxSandboxOptions.DefaultCommand,
                Memory =
                    builder.Configuration.GetValue<string?>(MemoryKey)
                    ?? SbxSandboxOptions.DefaultMemory,
                InjectedSecrets =
                    builder.Configuration.GetSection(InjectedSecretsKey).Get<string[]>()
                    ?? SbxSandboxOptions.DefaultInjectedSecrets,
                AgentTemplates =
                    builder.Configuration.GetSection(AgentTemplatesKey).Get<string[]>()
                    ?? SbxSandboxOptions.DefaultAgentTemplates,
                SessionFiles = builder.Configuration.GetValue(CarrySessionKey, defaultValue: false)
                    ? builder.Configuration.GetSection(SessionFilesKey).Get<string[]>()
                        ?? SbxSandboxOptions.DefaultSessionFiles
                    : [],
            }
        );
        // The preview ledger belongs to the process that holds the sandboxes, and only that
        // process can honestly answer whether a Run has a window open (run-previews design D2).
        // Registered here rather than beside the module's unhosted default, so a habitat with no
        // launcher keeps answering "previews are not hosted here".
        builder.Services.AddSingleton<RunPreviewHost>();
        builder.Services.AddSingleton<BuildingBlocks.Agents.IRunPreviewMonitor>(provider =>
            provider.GetRequiredService<RunPreviewHost>()
        );

        builder.Services.AddSingleton<IAgentProcessHost, SbxAgentProcessHost>();
    }
}
