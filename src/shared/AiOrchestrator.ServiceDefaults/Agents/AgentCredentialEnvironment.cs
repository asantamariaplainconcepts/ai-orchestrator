using AiOrchestrator.BuildingBlocks.Agents;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// What credentials reach the agent's process, decided once for every runtime (design D2).
/// <para>
/// Two shapes, and the choice belongs to the host, not the runtime. A host that cannot
/// authenticate on the agent's behalf gets the values in the process environment for its
/// lifetime and nowhere else (BR-010) — the behaviour every habitat had before sandboxing. A
/// host that authenticates the agent's outbound requests itself gets <b>nothing</b>: handing
/// values to a boundary built so the agent cannot hold them would defeat the boundary.
/// </para>
/// </summary>
static class AgentCredentialEnvironment
{
    public static Dictionary<string, string> For(
        IAgentProcessHost processHost,
        AgentCredentials credentials,
        string aiKeyVariable
    )
    {
        if (processHost.SuppliesCredentials)
        {
            return [];
        }

        var environment = new Dictionary<string, string>
        {
            ["GITHUB_TOKEN"] = credentials.VendorAccessToken,
        };

        // Only when one was resolved (#279): an exported empty key shadows the CLI's own session
        // auth, which is exactly what the switched-off credential exists to use.
        if (!string.IsNullOrEmpty(credentials.AiApiKey))
        {
            environment[aiKeyVariable] = credentials.AiApiKey;
        }

        return environment;
    }
}
