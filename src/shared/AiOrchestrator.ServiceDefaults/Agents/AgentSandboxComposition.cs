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

    /// <summary>
    /// Azure Container Apps Sandboxes (#296): microVMs created over an authenticated API, so this
    /// habitat's agents run somewhere the executor is not, and no socket exists on either machine.
    /// </summary>
    public const string AcaLauncher = "aca";

    /// <summary>
    /// The Project's own SandboxGroup (#296 design D4). Per project rather than per deployment,
    /// because the platform scopes credentials to the group and #244 promises a Run bills as its
    /// own Project — a shared group would break that silently.
    /// </summary>
    public const string SandboxGroupKey = "Agents:Sandbox:Group";

    /// <summary>The disk image sandboxes boot from; the platform's prebuilt ones carry the CLIs.</summary>
    public const string DiskKey = "Agents:Sandbox:Disk";

    /// <summary>
    /// The hosts a sandbox may reach. Required rather than defaulted for the ACA launcher: deny by
    /// default is <b>opt-in</b> on that platform — measured 2026-08-08, a sandbox with no policy
    /// reached example.com and pypi.org — so a habitat that says nothing would run its agents
    /// unrestricted while believing otherwise.
    /// </summary>
    /// <summary>
    /// The group credential ids to attach to each sandbox — **ids, never values** (BR-010). Not
    /// refused when absent, unlike the egress list: a habitat whose agent authenticates some other
    /// way is legitimate, and a Run without a credential fails loudly at the agent rather than
    /// silently at the boundary.
    /// </summary>
    public const string CredentialsKey = "Agents:Sandbox:Credentials";

    public const string EgressAllowKey = "Agents:Sandbox:EgressAllow";

    internal static void AddAgentProcessHost(IHostApplicationBuilder builder)
    {
        var launcher = builder.Configuration.GetValue<string?>(LauncherKey);

        if (string.IsNullOrWhiteSpace(launcher))
        {
            builder.Services.AddSingleton<IAgentProcessHost, LocalAgentProcessHost>();
            return;
        }

        var sbx = string.Equals(launcher, SbxLauncher, StringComparison.OrdinalIgnoreCase);
        var aca = string.Equals(launcher, AcaLauncher, StringComparison.OrdinalIgnoreCase);

        if (!sbx && !aca)
        {
            throw new InvalidOperationException(
                $"'{LauncherKey}' is set to '{launcher}', which is not a launcher this build "
                    + $"knows. The supported values are '{SbxLauncher}' and '{AcaLauncher}'. "
                    + "Remove the key to run the agent as a child of this process."
            );
        }

        // The preview ledger belongs to the process that holds the sandboxes, and only that
        // process can honestly answer whether a Run has a window open (run-previews design D2).
        // Registered before either launcher's options, because both hosts take it.
        builder.Services.AddSingleton<RunPreviewHost>();
        builder.Services.AddSingleton<BuildingBlocks.Agents.IRunPreviewMonitor>(provider =>
            provider.GetRequiredService<RunPreviewHost>()
        );

        if (aca)
        {
            AddAca(builder);
            return;
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
        builder.Services.AddSingleton<IAgentProcessHost, SbxAgentProcessHost>();
    }

    /// <summary>
    /// The Azure launcher (#296). Two of its settings are <b>required</b> rather than defaulted,
    /// which is unusual here and deliberate: they correct platform defaults that are actively wrong
    /// for a Run, and both were found by exercise rather than documentation. A habitat that leaves
    /// them unsaid would run agents it believes are constrained and are not, so composition refuses
    /// instead of choosing on its behalf (ADR-0010: asked, never inferred).
    /// </summary>
    static void AddAca(IHostApplicationBuilder builder)
    {
        var group = builder.Configuration.GetValue<string?>(SandboxGroupKey);
        if (string.IsNullOrWhiteSpace(group))
        {
            throw new InvalidOperationException(
                $"This habitat runs agents in Azure sandboxes ({LauncherKey} = {AcaLauncher}) but "
                    + $"names no sandbox group ('{SandboxGroupKey}'). A group is per Project, so a "
                    + "Run bills and acts as its own Project's identity rather than a shared one."
            );
        }

        var allow = builder.Configuration.GetSection(EgressAllowKey).Get<string[]>();
        if (allow is null || allow.Length == 0)
        {
            throw new InvalidOperationException(
                $"This habitat runs agents in Azure sandboxes but declares no egress allow list "
                    + $"('{EgressAllowKey}'). Deny-by-default is opt-in on that platform — a "
                    + "sandbox created without a policy has unrestricted outbound access — so a "
                    + "habitat that says nothing would run its agents unrestricted while believing "
                    + $"otherwise. Declare the hosts they may reach, starting with "
                    + $"[{string.Join(", ", AcaSandboxOptions.DefaultEgressAllow)}]."
            );
        }

        builder.Services.AddSingleton(
            new AcaSandboxOptions
            {
                CommandPath =
                    builder.Configuration.GetValue<string?>(CommandPathKey)
                    ?? AcaSandboxOptions.DefaultCommand,
                SandboxGroup = group,
                Disk =
                    builder.Configuration.GetValue<string?>(DiskKey)
                    ?? AcaSandboxOptions.DefaultDisk,
                EgressAllow = allow,
                Credentials =
                    builder.Configuration.GetSection(CredentialsKey).Get<string[]>() ?? [],
            }
        );

        builder.Services.AddSingleton<IAgentProcessHost, AcaAgentProcessHost>();
    }
}
