using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents;

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
/// </summary>
sealed class SbxAgentProcessHost(SbxSandboxOptions options, ILogger<SbxAgentProcessHost> logger)
    : IAgentProcessHost
{
    /// <summary>
    /// The proxy authenticates the agent's outbound requests from the host's own keychain, so
    /// no value is handed in (design D2, spike-verified).
    /// </summary>
    public bool SuppliesCredentials => true;

    public string CredentialSource =>
        "the sandbox host's stored credentials, injected at egress — no value entered the sandbox";

    public async Task<AgentProcessOutcome> Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? onOutput = null
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

        await EnsureReady(cancellationToken);

        var sandbox = $"aio-run-{Guid.NewGuid():N}"[..24];

        await Create(sandbox, workingDirectory, fileName, cancellationToken);
        try
        {
            // The workspace must be visible at the path the command will use before the agent
            // runs (design D4): a wrong mapping has to fail here, naming itself, rather than as
            // an agent confused about a missing repository.
            await VerifyWorkspace(sandbox, workingDirectory, cancellationToken);

            return await Exec(
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
            // An abandoned sandbox is the leak; the Run's truth is in the database. Disposal
            // survives cancellation on purpose — the PodRunLauncher precedent.
            await Dispose(sandbox);
        }
    }

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
    public async Task<bool> CliAnswers(string command, CancellationToken cancellationToken)
    {
        if (
            _cliAnswers.TryGetValue(command, out var cached)
            && !cached.IsStale(options.CliProbeInterval)
        )
        {
            return cached.Answered;
        }

        var answered = await AskInASandbox(command, cancellationToken);
        _cliAnswers[command] = new CliVerdict(answered, DateTimeOffset.UtcNow);
        return answered;
    }

    readonly System.Collections.Concurrent.ConcurrentDictionary<string, CliVerdict> _cliAnswers =
        new(StringComparer.Ordinal);

    sealed record CliVerdict(bool Answered, DateTimeOffset At)
    {
        public bool IsStale(TimeSpan after) => DateTimeOffset.UtcNow - At > after;
    }

    async Task<bool> AskInASandbox(string command, CancellationToken cancellationToken)
    {
        var sandbox = $"aio-probe-{Guid.NewGuid():N}"[..24];

        try
        {
            await Create(sandbox, Path.GetTempPath(), command, cancellationToken);
        }
        catch (AgentProcessHostException)
        {
            // The host itself is the problem, and CheckReadiness already says so with its
            // remedy. Reporting the CLI as absent too would print two sentences for one fault.
            return false;
        }

        try
        {
            var version = await Sbx(
                ["exec", sandbox, command, "--version"],
                Brief,
                cancellationToken
            );
            return !version.TimedOut && version.ExitCode == 0;
        }
        finally
        {
            await Dispose(sandbox);
        }
    }

    /// <summary>
    /// The preconditions, before a Run's agent starts (design D2's failure mode). A launcher
    /// that claims injection while holding no credential would start an unauthenticated agent
    /// that fails deep inside the Run for a reason reading like a repository problem.
    /// </summary>
    async Task EnsureReady(CancellationToken cancellationToken)
    {
        var daemon = await Sbx(["daemon", "status"], Brief, cancellationToken);
        if (daemon.ExitCode != 0)
        {
            throw new AgentProcessHostException(
                "The sandbox daemon is not running, so no Run can execute here. Start it with "
                    + $"`{options.CommandPath} daemon start`. ({Detail(daemon)})"
            );
        }

        if (options.InjectedSecrets.Count == 0)
        {
            return;
        }

        var secrets = await Sbx(["secret", "ls"], Brief, cancellationToken);
        if (secrets.ExitCode != 0)
        {
            throw new AgentProcessHostException(
                "The sandbox host's stored credentials could not be read, so whether the agent "
                    + $"can authenticate is unknown. ({Detail(secrets)})"
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

    async Task Create(
        string sandbox,
        string workspace,
        string command,
        CancellationToken cancellationToken
    )
    {
        var created = await Sbx(
            [
                "run",
                "-d",
                "--name",
                sandbox,
                "--memory",
                options.Memory,
                Template(command),
                workspace,
            ],
            options.CreateTimeout,
            cancellationToken
        );

        if (created.ExitCode != 0)
        {
            throw new AgentProcessHostException(
                $"The sandbox for this Run could not be created. ({Detail(created)})"
            );
        }

        SandboxLog.Created(logger, sandbox);
    }

    async Task VerifyWorkspace(
        string sandbox,
        string workspace,
        CancellationToken cancellationToken
    )
    {
        var seen = await Sbx(["exec", sandbox, "test", "-d", workspace], Brief, cancellationToken);

        if (seen.ExitCode != 0)
        {
            throw new AgentProcessHostException(
                $"The Run's workspace ({workspace}) is not visible inside its sandbox, so the "
                    + "agent would report a repository that is not there. The sandbox mounts the "
                    + "host path at the same location, which means this path is not one the "
                    + $"sandbox host can reach. ({Detail(seen)})"
            );
        }
    }

    async Task<AgentProcessOutcome> Exec(
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

    async Task Dispose(string sandbox)
    {
        // --force because sbx refuses a prompt off a tty (spike H4), and CancellationToken.None
        // because the disposal must happen even when the Run was cancelled.
        var removed = await Sbx(["rm", "--force", sandbox], Brief, CancellationToken.None);

        if (removed.ExitCode != 0)
        {
            // Not the Run's failure: its outcome is already decided. But a sandbox that outlives
            // its Run is a leak an operator needs to know about.
            SandboxLog.DisposalFailed(logger, sandbox, Detail(removed));
            return;
        }

        SandboxLog.Disposed(logger, sandbox);
    }

    async Task<AgentProcessOutcome> Sbx(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        try
        {
            // Awaited inside the try on purpose: HeadlessProcess.Run is async, so returning its
            // task unawaited would put the Win32Exception in the task and sail straight past
            // this catch — the caller would then see a raw ENOENT instead of the remedy.
            return await HeadlessProcess.Run(
                options.CommandPath,
                arguments,
                // The CLI's own working directory is irrelevant to what it does; the sandbox's
                // workspace travels as an argument.
                Environment.CurrentDirectory,
                new Dictionary<string, string>(),
                timeout,
                cancellationToken
            );
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            // The binary is not there at all — the raw ENOENT tells nobody anything (#279).
            throw new AgentProcessHostException(
                $"This habitat executes agents in sandboxes ({AgentSandboxComposition.LauncherKey}"
                    + $" = {AgentSandboxComposition.SbxLauncher}), but the sbx CLI could not be "
                    + $"started at '{options.CommandPath}'. Install it, or name its path in "
                    + $"'{AgentSandboxComposition.CommandPathKey}'. ({exception.Message})"
            );
        }
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

    /// <summary>Bounded so a hung CLI cannot hold a Run open; long enough for a cold daemon.</summary>
    static readonly TimeSpan Brief = TimeSpan.FromSeconds(30);

    static string Detail(AgentProcessOutcome outcome) =>
        outcome.TimedOut ? "the sbx CLI did not answer in time"
        : string.IsNullOrWhiteSpace(outcome.Stderr) ? $"exit {outcome.ExitCode}"
        : $"exit {outcome.ExitCode}: {Truncate(outcome.Stderr)}";

    static string Truncate(string text) =>
        text.Length <= 500 ? text.Trim() : text[..500].Trim() + " …(truncated)";
}

/// <summary>
/// Everything the sbx host knows, decided in composition. Deliberately NOT IConfiguration
/// (design D7): the driver cannot read a connection string it has no access to.
/// </summary>
sealed class SbxSandboxOptions
{
    public const string DefaultCommand = "sbx";

    /// <summary>
    /// Explicit rather than sbx's 50%-of-host-RAM default (spike H5), which two concurrent
    /// sandboxes would use to exhaust the machine.
    /// </summary>
    public const string DefaultMemory = "4g";

    /// <summary>
    /// GitHub by default: every Run's agent does git work, and this is the credential whose
    /// injection the spike proved end to end. The AI provider is habitat-dependent — a free
    /// model (DEC-044) needs none — so it is named by configuration or not at all.
    /// </summary>
    public static readonly string[] DefaultInjectedSecrets = ["github"];

    public required string CommandPath { get; init; }
    public required string Memory { get; init; }
    public required IReadOnlyList<string> InjectedSecrets { get; init; }

    /// <summary>
    /// The commands sbx provides an agent template for — the images that actually contain the
    /// CLI. Configured rather than hardcoded so a new runtime does not need a code change, and
    /// defaulted to this product's two runtimes (DEC-012, DEC-044), whose names sbx happens to
    /// share because both are naming the same CLIs.
    /// </summary>
    public IReadOnlyList<string> AgentTemplates { get; init; } = DefaultAgentTemplates;

    public static readonly string[] DefaultAgentTemplates = ["claude", "opencode"];

    /// <summary>Warm creation was ~4.5s in the spike; a cold image pull is minutes.</summary>
    public TimeSpan CreateTimeout { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How long a CLI-in-the-template verdict stands before it is asked again. Far longer than
    /// the panel's 30s cadence on purpose: this answer belongs to the image, and re-creating a
    /// sandbox every cycle to re-learn it would spend seconds of every thirty on a question
    /// whose answer cannot have changed.
    /// </summary>
    public TimeSpan CliProbeInterval { get; init; } = TimeSpan.FromMinutes(15);
}

static partial class SandboxLog
{
    [LoggerMessage(
        EventId = 4110,
        Level = LogLevel.Information,
        Message = "Created sandbox {Sandbox} for an agent"
    )]
    public static partial void Created(ILogger logger, string sandbox);

    [LoggerMessage(
        EventId = 4111,
        Level = LogLevel.Information,
        Message = "Removed sandbox {Sandbox}"
    )]
    public static partial void Disposed(ILogger logger, string sandbox);

    [LoggerMessage(
        EventId = 4112,
        Level = LogLevel.Error,
        Message = "Sandbox {Sandbox} outlived its Run and could not be removed: {Detail}"
    )]
    public static partial void DisposalFailed(ILogger logger, string sandbox, string detail);
}
