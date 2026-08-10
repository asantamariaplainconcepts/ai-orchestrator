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

        var environment = new Dictionary<string, string>();

        // Each only when there is a value to carry — an exported empty variable SHADOWS whatever
        // auth the host's own tooling holds, which is the opposite of what an unset credential
        // means. #279 established the rule for the AI key (the switched-off state runs on the
        // machine's own session); #244 AC6 extended it to the vendor token, which a Local Run
        // never resolves.
        if (!string.IsNullOrEmpty(credentials.VendorAccessToken))
        {
            environment["GITHUB_TOKEN"] = credentials.VendorAccessToken;
        }

        if (!string.IsNullOrEmpty(credentials.AiApiKey))
        {
            environment[aiKeyVariable] = credentials.AiApiKey;
        }

        return environment;
    }
}
