using AiOrchestrator.BuildingBlocks.Agents;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents.Sbx;

/// <summary>
/// The agent CLI inside a Docker Sandboxes microVM, one sandbox per Run (design D3).
/// <para>
/// Every mechanic here was observed on real hardware by the sbx spike
/// (<c>openspec/changes/archive/2026-08-07-spike-sbx-sandbox/findings.md</c>), not read from
/// documentation: the workspace arrives over virtiofs at the same absolute path; <c>sbx exec</c>
/// carries an inner exit code and both streams back verbatim; <c>rm</c> refuses off a tty
/// without <c>--force</c>; and a stored service secret never enters the sandbox at all — the
/// host-side proxy authenticates the agent's requests, so <c>GITHUB_TOKEN</c> inside is empty
/// while a clone of a private repository still succeeds.
/// </para>
/// <para>
/// It is constructed with its own options and nothing else (design D7): there is no path by
/// which a connection string or a secret-store location could reach the sandbox, because this
/// class cannot see them.
/// </para>
/// <para>
/// This is the orchestrator: it wires together the collaborators that actually do the work —
/// <see cref="SbxCli"/> (the process seam), <see cref="SbxSandboxLifecycle"/> (create/exec/
/// dispose), <see cref="SbxSessionCarriage"/> (#288's copy-in), <see cref="SbxPreviewPublisher"/>
/// (run-previews), and <see cref="SbxCliProbe"/> (readiness's CLI/model asks) — and keeps only
/// the sequencing that belongs to <see cref="Run"/> itself.
/// </para>
/// </summary>
sealed class SbxAgentProcessHost : IAgentProcessHost
{
    readonly RunPreviewHost _previews;
    readonly RunSandboxHost _sandboxes;
    readonly SbxSessionCarriage _sessionCarriage;
    readonly SbxSandboxLifecycle _lifecycle;
    readonly SbxCliProbe _probe;

    public SbxAgentProcessHost(
        SbxSandboxOptions options,
        RunPreviewHost previews,
        RunSandboxHost sandboxes,
        ILogger<SbxAgentProcessHost> logger
    )
    {
        _previews = previews;
        _sandboxes = sandboxes;

        var cli = new SbxCli(options);
        var previewPublisher = new SbxPreviewPublisher(cli, previews, logger);
        _sessionCarriage = new SbxSessionCarriage(options, cli, logger);
        _lifecycle = new SbxSandboxLifecycle(
            options,
            cli,
            logger,
            _sessionCarriage,
            previewPublisher
        );
        _probe = new SbxCliProbe(options, cli, _lifecycle, _sessionCarriage);
    }

    /// <summary>
    /// The proxy authenticates the agent's outbound requests from the host's own keychain, so
    /// no value is handed in (design D2, spike-verified).
    /// </summary>
    public bool SuppliesCredentials => true;

    public string CredentialSource =>
        _sessionCarriage.HasCarriedFiles
            // The third source (#288). Named as the machine owner's own seat because that is what
            // a reader needs to know when a Run's spend appears on their account.
            ? "the machine owner's own session, copied into the sandbox — this Run acts as that seat"
            : "injected at egress by the sandbox host — no value enters the sandbox";

    public SessionCarriageGap? SessionUnavailableFor(
        string runtimeName,
        string command,
        string? credentialSecretName
    ) => _sessionCarriage.SessionUnavailableFor(runtimeName, command, credentialSecretName);

    public async Task<AgentProcessOutcome> Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? onOutput = null,
        RunPreview? preview = null,
        // Ignored: sbx's sandboxes are local and its isolation is not scoped by Project.
        Guid? projectId = null,
        // Published, so a human can open a terminal in this Run's sandbox (#304). Null means the
        // caller did not name the Run, and nothing is published — the terminal then answers "this
        // Run has no sandbox" rather than addressing the wrong one.
        Guid? runId = null
    )
    {
        // The contract, asserted rather than trusted: a caller that still passes values would be
        // handing them to a boundary built so the agent cannot hold them. AgentCredentialEnvironment
        // already returns empty for this host — this catches a future caller that forgets.
        if (environment.Count > 0)
        {
            throw new AgentProcessHostException(
                "The sandbox host was given credential values to pass into the sandbox, which "
                    + "would defeat the boundary it exists to create. This is a composition fault, "
                    + "not a configuration one."
            );
        }

        await _lifecycle.EnsureReady(cancellationToken);

        var sandbox = $"aio-run-{Guid.NewGuid():N}"[..24];

        await _lifecycle.Create(sandbox, workingDirectory, fileName, preview, cancellationToken);

        // Beside creation, never anywhere else (#304). The pairing is true from the moment the
        // sandbox exists until the `finally` below removes it, which is exactly as long as a
        // terminal could be open in it.
        if (runId is { } run)
        {
            _sandboxes.Created(run, sandbox);
        }

        try
        {
            // The workspace must be visible at the path the command will use before the agent
            // runs (design D4): a wrong mapping has to fail here, naming itself, rather than as
            // an agent confused about a missing repository.
            await _lifecycle.VerifyWorkspace(sandbox, workingDirectory, cancellationToken);

            return await _lifecycle.Exec(
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
            // The preview dies with the sandbox, in the same finally, so no code path exists in
            // which the record outlives what it describes (run-previews design D1/D2). Removed
            // BEFORE disposal is attempted: a failed removal must not leave a reachable-looking
            // entry pointing at a port nothing serves.
            if (preview is not null)
            {
                _previews.Gone(preview.RunId);
            }

            // The same reasoning, one line later: a name that outlived its sandbox would point a
            // terminal at a microVM that is gone, which is worse than having no name at all.
            if (runId is { } finished)
            {
                _sandboxes.Gone(finished);
            }

            // An abandoned sandbox is the leak; the Run's truth is in the database. Disposal
            // survives cancellation on purpose: a cancelled Run must not leak a sandbox.
            await _lifecycle.Dispose(sandbox);
        }
    }

    public Task<AgentHostReadiness> CheckReadiness(CancellationToken cancellationToken) =>
        _lifecycle.CheckReadiness(cancellationToken);

    /// <summary>
    /// Whether the runtime's CLI answers inside a sandbox — the only machine whose answer a Run
    /// depends on (design D6).
    /// <para>
    /// On a cadence of its own, deliberately. Creating a sandbox costs seconds (spike H5), and
    /// what this asks is a property of the <b>template image</b>: it changes when the image
    /// changes, not between two probes thirty seconds apart. Asking every cycle would spend a
    /// third of the machine's time re-answering a question whose answer cannot have moved. The
    /// preconditions above are the ones probed at full cadence, because those do move.
    /// </para>
    /// </summary>
    public Task<bool> CliAnswers(string command, CancellationToken cancellationToken) =>
        _probe.CliAnswers(command, cancellationToken);

    public Task<IReadOnlyList<string>?> ListModels(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken
    ) => _probe.ListModels(command, arguments, cancellationToken);
}
