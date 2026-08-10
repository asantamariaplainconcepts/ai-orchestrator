using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents.Sbx;

/// <summary>
/// Creates, verifies, executes in, and disposes of one sbx sandbox — the microVM lifecycle a
/// Run (and the readiness/CLI probes) sit on top of.
/// </summary>
sealed class SbxSandboxLifecycle(
    SbxSandboxOptions options,
    SbxCli cli,
    ILogger logger,
    SbxSessionCarriage sessionCarriage,
    SbxPreviewPublisher previewPublisher
)
{
    /// <summary>
    /// The host's own preconditions for the panel (design D6), on its 30s cadence — these are
    /// the facts that change minute to minute. The refusal sentences are the same ones a Run
    /// would fail with, so the panel and the failure cannot drift.
    /// </summary>
    public async Task<AgentHostReadiness> CheckReadiness(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureReady(cancellationToken);
            return new AgentHostReadiness(
                Ready: true,
                Where: "a per-Run sandbox on this machine",
                Remedy: null
            );
        }
        catch (AgentProcessHostException refusal)
        {
            return new AgentHostReadiness(
                Ready: false,
                Where: "a per-Run sandbox on this machine",
                Remedy: refusal.Message
            );
        }
    }

    /// <summary>
    /// The preconditions, before a Run's agent starts (design D2's failure mode). A launcher
    /// that claims injection while holding no credential would start an unauthenticated agent
    /// that fails deep inside the Run for a reason reading like a repository problem.
    /// </summary>
    public async Task EnsureReady(CancellationToken cancellationToken)
    {
        var daemon = await cli.Run(["daemon", "status"], SbxCli.Brief, cancellationToken);
        if (daemon.ExitCode != 0)
        {
            throw new AgentProcessHostException(
                "The sandbox daemon is not running, so no Run can execute here. Start it with "
                    + $"`{options.CommandPath} daemon start`. ({SbxCli.Detail(daemon)})"
            );
        }

        await ReapAbandoned(cancellationToken);

        if (options.InjectedSecrets.Count == 0)
        {
            return;
        }

        var secrets = await cli.Run(["secret", "ls"], SbxCli.Brief, cancellationToken);
        if (secrets.ExitCode != 0)
        {
            throw new AgentProcessHostException(
                "The sandbox host's stored credentials could not be read, so whether the agent "
                    + $"can authenticate is unknown. ({SbxCli.Detail(secrets)})"
            );
        }

        var missing = options
            .InjectedSecrets.Where(secret =>
                !secrets.Stdout.Contains(secret, StringComparison.OrdinalIgnoreCase)
            )
            .ToArray();

        if (missing.Length > 0)
        {
            throw new AgentProcessHostException(
                $"The sandbox host holds no credential for {string.Join(", ", missing)}, and this "
                    + "habitat expects it to authenticate the agent. The agent would run "
                    + "unauthenticated and fail for an unrelated-looking reason. Store it with "
                    + $"`{options.CommandPath} secret set -g {missing[0]}` — the value stays in the "
                    + "host's keychain and never enters a sandbox."
            );
        }
    }

    /// <summary>Sandboxes this host abandoned in a previous life, swept once per process.</summary>
    int _reaped;

    /// <summary>
    /// Removes sandboxes this host's previous process left behind, once, before the first sandbox
    /// of this one.
    /// <para>
    /// <b>Why a `finally` is not enough, measured 2026-08-09.</b> The developer's machine held
    /// <b>31 running sandboxes and 125 GB of disk</b>, 25 of them <c>aio-probe-*</c> — one per
    /// readiness sweep, created every 30 seconds. Every creation here is already paired with a
    /// disposal in a <c>finally</c>, and that pairing is correct: what it cannot survive is the
    /// process not being there to run it. Stop `aspire run` mid-sweep, or kill the host, and the
    /// microVM outlives the only reference anyone had to it. Over a week of dev-loop restarts
    /// that is a full disk, and no amount of in-process discipline prevents it.
    /// </para>
    /// <para>
    /// So the namespace is claimed rather than merely tidied: <c>aio-probe-*</c> and
    /// <c>aio-run-*</c> belong to this host, and a fresh process starts by removing whatever
    /// carries those names. <b>The constraint that buys:</b> two orchestrators sharing one machine
    /// would reap each other's live Runs. That is out of scope by DEC-016 — one owner, one
    /// machine — and it is written here rather than discovered.
    /// </para>
    /// </summary>
    async Task ReapAbandoned(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _reaped, 1) == 1)
        {
            return;
        }

        var listed = await cli.Run(["ls"], SbxCli.Brief, cancellationToken);
        if (listed.ExitCode != 0)
        {
            return;
        }

        var abandoned = listed
            .Stdout.Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .Where(name =>
                name is not null
                && (
                    name.StartsWith("aio-probe-", StringComparison.Ordinal)
                    || name.StartsWith("aio-run-", StringComparison.Ordinal)
                )
            )
            .ToArray();

        foreach (var name in abandoned)
        {
            await Dispose(name!);
        }

        if (abandoned.Length > 0)
        {
            SandboxLog.Reaped(logger, abandoned.Length);
        }
    }

    public async Task Create(
        string sandbox,
        string workspace,
        string command,
        BuildingBlocks.Agents.RunPreview? preview,
        CancellationToken cancellationToken
    )
    {
        // The host port is OMITTED, which is sbx's ephemeral form — `-p 0:<port>` is rejected
        // outright ("port 0 out of range", observed 2026-08-07). Ephemeral because N concurrent
        // Runs must not contend for one number, and loopback-bound because only this machine's
        // Server relays it.
        string[] publish = preview is null ? [] : ["-p", preview.SandboxPort.ToString()];

        var created = await cli.Run(
            [
                "run",
                "-d",
                "--name",
                sandbox,
                "--memory",
                options.Memory,
                .. publish,
                Template(command),
                workspace,
            ],
            options.CreateTimeout,
            cancellationToken
        );

        if (created.ExitCode != 0)
        {
            throw new AgentProcessHostException(
                $"The sandbox for this Run could not be created. ({SbxCli.Detail(created)})"
            );
        }

        SandboxLog.Created(logger, sandbox);

        // **Everything past this point owns a live microVM.** Creation sits outside every
        // caller's `finally` — `Run`'s and the probe's alike — so a throw between "the sandbox
        // exists" and "the caller has its name" would leave one alive with nobody holding a
        // reference to delete it. `RecordPreview` is the step that can do it; `CarrySession`
        // logs and continues rather than throwing, so it never has.
        //
        // Unwound here rather than at each call site, because the half-built object is this
        // method's to finish or to undo.
        //
        // This is a hole worth closing and it is **not** what filled the developer's disk — see
        // `ReapAbandoned` for that, which is a process dying, not an exception.
        try
        {
            await sessionCarriage.Carry(sandbox, cancellationToken);

            if (preview is not null)
            {
                await previewPublisher.Record(sandbox, preview, cancellationToken);
            }
        }
        catch
        {
            await Dispose(sandbox);
            throw;
        }
    }

    public async Task VerifyWorkspace(
        string sandbox,
        string workspace,
        CancellationToken cancellationToken
    )
    {
        var seen = await cli.Run(
            ["exec", sandbox, "test", "-d", workspace],
            SbxCli.Brief,
            cancellationToken
        );

        if (seen.ExitCode != 0)
        {
            throw new AgentProcessHostException(
                $"The Run's workspace ({workspace}) is not visible inside its sandbox, so the "
                    + "agent would report a repository that is not there. The sandbox mounts the "
                    + "host path at the same location, which means this path is not one the "
                    + $"sandbox host can reach. ({SbxCli.Detail(seen)})"
            );
        }
    }

    public async Task<AgentProcessOutcome> Exec(
        string sandbox,
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? onOutput
    )
    {
        // The agent's own timeout is BR-005's, and it must kill the process INSIDE the sandbox —
        // so it is handed to the exec, whose cancellation ends the exec and whose sandbox is
        // then disposed by the finally above.
        string[] exec = ["exec", "--workdir", workingDirectory, sandbox, fileName, .. arguments];

        return await HeadlessProcess.Run(
            options.CommandPath,
            exec,
            workingDirectory,
            // Nothing (design D7): the sbx CLI runs on this machine, and what it carries in is
            // only what these arguments say.
            new Dictionary<string, string>(),
            timeout,
            cancellationToken,
            onOutput
        );
    }

    public async Task Dispose(string sandbox)
    {
        // --force because sbx refuses a prompt off a tty (spike H4), and CancellationToken.None
        // because the disposal must happen even when the Run was cancelled.
        var removed = await cli.Run(
            ["rm", "--force", sandbox],
            SbxCli.Brief,
            CancellationToken.None
        );

        if (removed.ExitCode != 0)
        {
            // Not the Run's failure: its outcome is already decided. But a sandbox that outlives
            // its Run is a leak an operator needs to know about.
            SandboxLog.DisposalFailed(logger, sandbox, SbxCli.Detail(removed));
            return;
        }

        SandboxLog.Disposed(logger, sandbox);
    }

    /// <summary>
    /// The sandbox image, chosen by the CLI that must exist inside it. sbx names its templates
    /// after the agents they carry, and those names are exactly this product's runtime commands
    /// — so a Run's own command selects the image that contains it.
    /// <para>
    /// This is not cosmetic. The generic <c>shell</c> template carries no agent CLI at all
    /// (observed 2026-08-07: <c>command -v claude</c> answers nothing in it, while the claude
    /// template answers 2.1.221), so a sandbox created generically would run every Run with a
    /// missing binary. Anything sbx has no template for falls back to <c>shell</c>, which is
    /// correct for the probe's own errands and for a command that needs no agent image.
    /// </para>
    /// </summary>
    string Template(string command) =>
        options.AgentTemplates.Contains(command, StringComparer.Ordinal) ? command : "shell";
}
