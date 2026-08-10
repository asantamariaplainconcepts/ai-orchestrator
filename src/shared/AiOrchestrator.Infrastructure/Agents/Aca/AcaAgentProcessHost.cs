using AiOrchestrator.BuildingBlocks.Agents;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents.Aca;

/// <summary>
/// The agent CLI inside an Azure Container Apps Sandbox — a hardware-isolated microVM created over
/// an authenticated API rather than a socket on this machine (#296, design D1).
/// <para>
/// It exists to retire the pod substrate, whose own requirement called its docker-socket grant
/// root-equivalent on the host and whose container shared that host's kernel. Nothing here needs a
/// grant on the executing machine at all, and — the property that made the whole thing possible —
/// the workspace is <b>sent</b> rather than mounted, so this host and the executor no longer have
/// to share a machine.
/// </para>
/// <para>
/// Everything asserted in these comments was measured in
/// <c>spike-azure-container-apps-sandboxes</c>; where a number appears it has a command and a date
/// behind it there rather than a vendor's summary.
/// </para>
/// <para>
/// This is the orchestrator: it wires together the collaborators that do the work —
/// <see cref="AcaCli"/> (the process seam), <see cref="AcaSandboxLifecycle"/> (create/dispose/
/// readiness), <see cref="AcaEgressPolicy"/> (the platform-default corrections), <see
/// cref="AcaWorkspaceTransfer"/> (tar → copy → untar), <see cref="AcaDetachedExecution"/> (the
/// detach-and-poll launcher seam), <see cref="AcaPreviewPublisher"/> (run-previews), and <see
/// cref="AcaCliProbe"/> (readiness's CLI/model asks) — and keeps only <see cref="Run"/>'s own
/// sequencing.
/// </para>
/// </summary>
sealed class AcaAgentProcessHost : IAgentProcessHost
{
    readonly RunPreviewHost _previews;
    readonly AcaSandboxLifecycle _lifecycle;
    readonly AcaEgressPolicy _egress;
    readonly AcaWorkspaceTransfer _workspace;
    readonly AcaDetachedExecution _execution;
    readonly AcaPreviewPublisher _previewPublisher;
    readonly AcaCliProbe _probe;

    public AcaAgentProcessHost(
        AcaSandboxOptions options,
        RunPreviewHost previews,
        ILogger<AcaAgentProcessHost> logger
    )
    {
        _previews = previews;

        var cli = new AcaCli(options);
        _lifecycle = new AcaSandboxLifecycle(options, cli, logger);
        _egress = new AcaEgressPolicy(options, cli);
        _workspace = new AcaWorkspaceTransfer(options, cli);
        _execution = new AcaDetachedExecution(options, cli);
        _previewPublisher = new AcaPreviewPublisher(cli, previews, logger);
        _probe = new AcaCliProbe(options, cli, _lifecycle);
    }

    /// <summary>
    /// The platform attaches credentials at its egress proxy, so no value is handed in — the same
    /// promise sbx's sentinel makes, by a different mechanism (design D4).
    /// </summary>
    public bool SuppliesCredentials => true;

    public string CredentialSource =>
        "injected at the sandbox platform's egress boundary — no value enters the sandbox";

    /// <summary>
    /// The question does not arise: there is no machine owner on a remote host, so #288's session
    /// carriage cannot exist here and no runtime is excluded by it.
    /// </summary>
    public SessionCarriageGap? SessionUnavailableFor(
        string runtimeName,
        string command,
        string? credentialSecretName
    ) => null;

    public async Task<AgentProcessOutcome> Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? onOutput = null,
        RunPreview? preview = null,
        Guid? projectId = null
    )
    {
        // The same assertion the sbx host makes, for the same reason: a caller that still passes
        // values would be handing them to a boundary built so the agent cannot hold them.
        if (environment.Count > 0)
        {
            throw new AgentProcessHostException(
                "The sandbox host was given credential values to pass into the sandbox, which "
                    + "would defeat the boundary it exists to create. This is a composition fault, "
                    + "not a configuration one."
            );
        }

        var sandbox = await _lifecycle.Create(_lifecycle.GroupFor(projectId), cancellationToken);

        try
        {
            // Declared, never inherited (design D3). Both of these platform defaults are actively
            // wrong for a Run and both were found by exercise rather than documentation.
            await _egress.DisableAutoSuspend(sandbox, cancellationToken);
            await _egress.ApplyEgress(sandbox, cancellationToken);

            // Sent, not mounted — the property this whole change rests on.
            await _workspace.Send(sandbox, workingDirectory, cancellationToken);

            if (preview is not null)
            {
                await _previewPublisher.Publish(sandbox, preview, cancellationToken);
            }

            return await _execution.RunDetachedAndPoll(
                sandbox,
                fileName,
                arguments,
                workingDirectory,
                timeout,
                cancellationToken,
                onOutput
            );
        }
        finally
        {
            // Removed before disposal is attempted, so a failed delete cannot leave a
            // reachable-looking record pointing at a port nothing serves (run-previews D1/D2).
            if (preview is not null)
            {
                _previews.Gone(preview.RunId);
            }

            // Asked before the sandbox is deleted, because deletion takes the log with it — and
            // asked in the finally, because a Run that timed out or was cancelled is exactly when
            // "what did it reach for" is worth having.
            await _egress.ReportDeniedEgress(sandbox, onOutput);

            await _lifecycle.Dispose(sandbox);
        }
    }

    public Task<AgentHostReadiness> CheckReadiness(CancellationToken cancellationToken) =>
        _lifecycle.CheckReadiness(cancellationToken);

    public Task<bool> CliAnswers(string command, CancellationToken cancellationToken) =>
        _probe.CliAnswers(command, cancellationToken);

    public Task<IReadOnlyList<string>?> ListModels(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken
    ) => _probe.ListModels(command, arguments, cancellationToken);
}
