using AiOrchestrator.BuildingBlocks.Agents;
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
sealed class SbxAgentProcessHost(
    SbxSandboxOptions options,
    RunPreviewHost previews,
    ILogger<SbxAgentProcessHost> logger
) : IAgentProcessHost
{
    /// <summary>
    /// The proxy authenticates the agent's outbound requests from the host's own keychain, so
    /// no value is handed in (design D2, spike-verified).
    /// </summary>
    public bool SuppliesCredentials => true;

    public string CredentialSource =>
        options.SessionFiles.Count > 0
            // The third source (#288). Named as the machine owner's own seat because that is what
            // a reader needs to know when a Run's spend appears on their account.
            ? "the machine owner's own session, copied into the sandbox — this Run acts as that seat"
            : "injected at egress by the sandbox host — no value enters the sandbox";

    public async Task<AgentProcessOutcome> Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? onOutput = null,
        BuildingBlocks.Agents.RunPreview? preview = null
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

        await Create(sandbox, workingDirectory, fileName, preview, cancellationToken);
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
            // The preview dies with the sandbox, in the same finally, so no code path exists in
            // which the record outlives what it describes (run-previews design D1/D2). Removed
            // BEFORE disposal is attempted: a failed removal must not leave a reachable-looking
            // entry pointing at a port nothing serves.
            if (preview is not null)
            {
                previews.Gone(preview.RunId);
            }

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
            await Create(sandbox, Path.GetTempPath(), command, preview: null, cancellationToken);
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
        BuildingBlocks.Agents.RunPreview? preview,
        CancellationToken cancellationToken
    )
    {
        // The host port is OMITTED, which is sbx's ephemeral form — `-p 0:<port>` is rejected
        // outright ("port 0 out of range", observed 2026-08-07). Ephemeral because N concurrent
        // Runs must not contend for one number, and loopback-bound because only this machine's
        // Server relays it.
        string[] publish = preview is null ? [] : ["-p", preview.SandboxPort.ToString()];

        var created = await Sbx(
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
                $"The sandbox for this Run could not be created. ({Detail(created)})"
            );
        }

        SandboxLog.Created(logger, sandbox);

        await CarrySession(sandbox, cancellationToken);

        if (preview is not null)
        {
            await RecordPreview(sandbox, preview, cancellationToken);
        }
    }

    /// <summary>
    /// Why this runtime's session cannot travel, when carriage is on and it cannot (#288). Null
    /// where carriage is off — no promise was made — or where the runtime's credential is a file
    /// the copy reaches.
    /// </summary>
    public SessionCarriageGap? SessionUnavailableFor(string runtimeName, string command) =>
        options.SessionFiles.Count == 0 || !options.KeychainRuntimes.Contains(command)
            ? null
            : new SessionCarriageGap(
                AgentRuntimeRemedies.SessionCannotTravel(
                    command,
                    "this machine's keychain",
                    runtimeName,
                    command
                ),
                AgentRuntimeRemedies.StoreSandboxSecret(options.CommandPath, command)
            );

    /// <summary>
    /// Copies the machine owner's agent-CLI credentials into the sandbox, where the habitat asked
    /// for it (#288). A copy, never a mount: it lives exactly as long as the sandbox and an agent
    /// cannot write back into the developer's own session state.
    /// <para>
    /// Only the credential FILES travel, not the CLI's configuration tree — observed 2026-08-08:
    /// opencode's entire session is <c>~/.local/share/opencode/auth.json</c> at 950 bytes, while
    /// <c>~/.config/opencode</c> is over a gigabyte of caches that buy nothing. With that one file
    /// copied in, <c>opencode auth list</c> saw both configured providers and a headless run
    /// answered on the owner's GitHub Copilot seat with no API key anywhere.
    /// </para>
    /// <para>
    /// Deliberately silent about what it cannot carry: a credential held in an OS keychain — which
    /// is where Claude Code keeps its on macOS — has no file to copy, and saying so is the
    /// readiness panel's job, not a failure at Run time.
    /// </para>
    /// </summary>
    async Task CarrySession(string sandbox, CancellationToken cancellationToken)
    {
        foreach (var file in options.SessionFiles)
        {
            var source = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                file
            );

            if (!File.Exists(source))
            {
                // Not signed into that CLI, or it keeps its credential somewhere a copy cannot
                // reach. Neither is this method's problem to report.
                continue;
            }

            // `sbx cp` places it under the sandbox user's home, which is the only place a CLI
            // looks. A failure is logged and the Run proceeds: an absent session degrades to the
            // agent saying it is not logged in, which the panel already explains.
            var copied = await Sbx(
                ["cp", source, $"{sandbox}:{SandboxHome}/{file}"],
                Brief,
                cancellationToken
            );

            if (copied.ExitCode != 0)
            {
                SandboxLog.SessionNotCarried(logger, sandbox, file, Detail(copied));
                continue;
            }

            SandboxLog.SessionCarried(logger, sandbox, file);
        }
    }

    /// <summary>The sandbox user's home, where every CLI looks for its credential.</summary>
    const string SandboxHome = "/home/agent";

    /// <summary>
    /// Reads back the port sbx actually allocated and records it. A preview that cannot be
    /// resolved is NOT a Run failure: the agent's work is the Run, and a missing window is a
    /// missing window. It is logged and the Run proceeds without one, which the read then
    /// reports as no preview — the honest answer.
    /// </summary>
    async Task RecordPreview(
        string sandbox,
        BuildingBlocks.Agents.RunPreview preview,
        CancellationToken cancellationToken
    )
    {
        var listed = await Sbx(["ports", sandbox], Brief, cancellationToken);

        if (listed.ExitCode != 0 || HostPort(listed.Stdout, preview.SandboxPort) is not { } port)
        {
            SandboxLog.PreviewUnavailable(logger, sandbox, Detail(listed));
            return;
        }

        previews.Published(preview.RunId, port);
        SandboxLog.PreviewPublished(logger, sandbox, port);
    }

    /// <summary>
    /// `sbx ports` prints a table: HOST IP, HOST PORT, SANDBOX PORT, PROTOCOL — and lists the
    /// same mapping once per address family (127.0.0.1 and ::1), so the first row for our sandbox
    /// port is the answer and the rest are the same answer again.
    /// </summary>
    static int? HostPort(string stdout, int sandboxPort)
    {
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var columns = line.Split(
                (char[])[' ', '\t'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );

            if (
                columns.Length >= 3
                && int.TryParse(columns[1], out var host)
                && int.TryParse(columns[2], out var inside)
                && inside == sandboxPort
            )
            {
                return host;
            }
        }

        return null;
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

    /// <summary>
    /// Credential files, relative to the machine owner's home, to copy into each sandbox — the
    /// dev loop's opt-in (#288). Empty means carry nothing, which is every habitat but the dev
    /// loop, because a carried session is readable by whatever runs in the sandbox.
    /// <para>
    /// Files only, and only credential ones. Observed 2026-08-08: opencode's whole session is
    /// <c>.local/share/opencode/auth.json</c> (950 bytes); its <c>.config/opencode</c> tree is
    /// caches. Claude Code on macOS has no file at all — its credential is in the system
    /// keychain — so nothing here can carry it, and the readiness panel says so instead.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> SessionFiles { get; init; } = [];

    /// <summary>
    /// Runtimes whose session a copy cannot reach because the machine keeps it in a keychain
    /// rather than a file. Observed on macOS 2026-08-08: Claude Code has no
    /// <c>~/.claude/.credentials.json</c>, and copying its directory in produced "Not logged in".
    /// <para>
    /// Configuration rather than a constant because it is platform-dependent — the same CLI on
    /// Linux writes a credentials file, which this list would then wrongly exclude. Defaulted for
    /// the platform this was measured on, and overridable by whoever measures another.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> KeychainRuntimes { get; init; } =
        OperatingSystem.IsMacOS() ? ["claude"] : [];

    /// <summary>
    /// What the dev loop carries when it opts in: opencode's credential file, and GitHub
    /// Copilot's, which arrives with its runtime (#243). Both are files; Claude Code's macOS
    /// session is not, deliberately absent rather than silently ineffective.
    /// </summary>
    public static readonly string[] DefaultSessionFiles =
    [
        ".local/share/opencode/auth.json",
        ".config/github-copilot/apps.json",
        ".config/github-copilot/hosts.json",
    ];

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
        EventId = 4115,
        Level = LogLevel.Information,
        Message = "Carried {File} into sandbox {Sandbox} — this Run acts as the machine owner's session"
    )]
    public static partial void SessionCarried(ILogger logger, string sandbox, string file);

    [LoggerMessage(
        EventId = 4116,
        Level = LogLevel.Warning,
        Message = "Could not carry {File} into sandbox {Sandbox}, so its runtime may not be signed in: {Detail}"
    )]
    public static partial void SessionNotCarried(
        ILogger logger,
        string sandbox,
        string file,
        string detail
    );

    [LoggerMessage(
        EventId = 4113,
        Level = LogLevel.Information,
        Message = "Sandbox {Sandbox} is serving a preview on host port {Port}"
    )]
    public static partial void PreviewPublished(ILogger logger, string sandbox, int port);

    [LoggerMessage(
        EventId = 4114,
        Level = LogLevel.Warning,
        Message = "Sandbox {Sandbox} published a preview port that could not be resolved, so this Run has no preview: {Detail}"
    )]
    public static partial void PreviewUnavailable(ILogger logger, string sandbox, string detail);

    [LoggerMessage(
        EventId = 4112,
        Level = LogLevel.Error,
        Message = "Sandbox {Sandbox} outlived its Run and could not be removed: {Detail}"
    )]
    public static partial void DisposalFailed(ILogger logger, string sandbox, string detail);
}
