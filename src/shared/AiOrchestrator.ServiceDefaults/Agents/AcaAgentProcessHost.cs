using System.Globalization;
using AiOrchestrator.BuildingBlocks.Agents;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents;

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
/// </summary>
sealed class AcaAgentProcessHost(
    AcaSandboxOptions options,
    RunPreviewHost previews,
    ILogger<AcaAgentProcessHost> logger
) : IAgentProcessHost
{
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

        var sandbox = await Create(GroupFor(projectId), cancellationToken);

        try
        {
            // Declared, never inherited (design D3). Both of these platform defaults are actively
            // wrong for a Run and both were found by exercise rather than documentation.
            await DisableAutoSuspend(sandbox, cancellationToken);
            await ApplyEgress(sandbox, cancellationToken);

            // Sent, not mounted — the property this whole change rests on.
            await SendWorkspace(sandbox, workingDirectory, cancellationToken);

            if (preview is not null)
            {
                await PublishPreview(sandbox, preview, cancellationToken);
            }

            return await RunDetachedAndPoll(
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
                previews.Gone(preview.RunId);
            }

            await Dispose(sandbox);
        }
    }

    /// <summary>
    /// The decision this whole design turns on (design D2).
    /// <para>
    /// A single <c>aca sandbox exec</c> cannot hold a Run: measured 2026-08-08, it fails between
    /// <b>50 and 60 seconds</b> — three attempts at 60 s, three failures, each giving up at ~121 s
    /// with <i>retry policy expired</i> — while BR-005 allows a phase thirty minutes and #96 asks
    /// that its output be visible while it works.
    /// </para>
    /// <para>
    /// So the agent is started <b>detached</b>, writing to a file inside the sandbox, and this
    /// polls with short execs, forwarding what is new as it arrives. From the executor's side
    /// <c>Run()</c> still blocks and still streams: the ceiling is absorbed here and never reaches
    /// it, which is what keeps this a third implementation of the launcher seam rather than a new
    /// executor.
    /// </para>
    /// </summary>
    async Task<AgentProcessOutcome> RunDetachedAndPoll(
        string sandbox,
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? onOutput
    )
    {
        var log = $"/tmp/aio-run-{Guid.NewGuid():N}.log";
        var status = $"{log}.exit";

        // One shell line, because the detachment has to survive the exec that starts it: the exec
        // returns in a second, the agent does not. The exit code is written to its own file, so
        // "finished" and "still working" are a fact on disk rather than an inference from silence.
        var command =
            $"cd {Quote(workingDirectory)} && nohup sh -c {Quote($"{Argv(fileName, arguments)} > {log} 2>&1; echo $? > {status}")} > /dev/null 2>&1 &";

        var started = await Aca(
            ["sandbox", "exec", "--id", sandbox, "-c", command],
            cancellationToken
        );
        if (started.ExitCode != 0)
        {
            throw new AgentProcessHostException(
                $"The agent could not be started inside the sandbox. ({Detail(started)})"
            );
        }

        var deadline = DateTimeOffset.UtcNow + timeout;
        var forwarded = 0;

        while (true)
        {
            // BR-005 is enforced here rather than by the platform: the sandbox would happily hold
            // a runaway agent, and nothing retries afterwards (BR-004), so the phase's own bound
            // is what ends it.
            if (DateTimeOffset.UtcNow >= deadline)
            {
                var tail = await ReadFrom(sandbox, log, forwarded, cancellationToken);
                Forward(tail, onOutput, ref forwarded);
                return new AgentProcessOutcome(
                    TimedOut: true,
                    ExitCode: -1,
                    Stdout: string.Empty,
                    Stderr: "The agent exceeded this phase's timeout and its sandbox was disposed."
                );
            }

            var chunk = await ReadFrom(sandbox, log, forwarded, cancellationToken);
            Forward(chunk, onOutput, ref forwarded);

            var exit = await ReadFrom(sandbox, status, skip: 0, cancellationToken);
            if (!string.IsNullOrWhiteSpace(exit))
            {
                // One last read: work written between the previous poll and the exit file must not
                // be lost, or a Run's last words would depend on poll timing.
                var last = await ReadFrom(sandbox, log, forwarded, cancellationToken);
                Forward(last, onOutput, ref forwarded);

                var code = int.TryParse(
                    exit.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed
                )
                    ? parsed
                    : -1;

                return new AgentProcessOutcome(
                    TimedOut: false,
                    ExitCode: code,
                    Stdout: string.Empty,
                    Stderr: string.Empty
                );
            }

            await Task.Delay(options.PollInterval, cancellationToken);
        }
    }

    /// <summary>
    /// Everything after the lines already forwarded. Reading by line count rather than by byte
    /// offset because a partially written line would otherwise be forwarded twice — once truncated
    /// and once whole — and a watcher would see the agent stutter.
    /// </summary>
    async Task<string> ReadFrom(
        string sandbox,
        string path,
        int skip,
        CancellationToken cancellationToken
    )
    {
        var read = await Aca(
            ["sandbox", "exec", "--id", sandbox, "-c", $"tail -n +{skip + 1} {path} 2>/dev/null"],
            cancellationToken
        );

        return read.ExitCode == 0 ? read.Stdout : string.Empty;
    }

    static void Forward(string chunk, Action<string>? onOutput, ref int forwarded)
    {
        if (onOutput is null || chunk.Length == 0)
        {
            return;
        }

        // The last element of a split on a trailing newline is empty; a chunk that does not end in
        // one is a line still being written, and holding it back is what keeps a watcher from
        // seeing half a sentence.
        var lines = chunk.Split('\n');
        var complete = lines.Length - 1;

        for (var index = 0; index < complete; index++)
        {
            onOutput(lines[index]);
            forwarded++;
        }
    }

    // ---- The platform's defaults, corrected (design D3) ----

    /// <summary>
    /// Auto-suspend is on at 600 s by default, and "idle" means no calls from <b>outside</b>:
    /// measured 2026-08-08, a sandbox went <c>Stopped</c> at t+41 s with a 60 s timeout <b>while a
    /// process wrote inside it every second</b>. An agent that thinks for ten minutes would be
    /// suspended mid-thought, so this is switched off for every sandbox a Run uses.
    /// </summary>
    async Task DisableAutoSuspend(string sandbox, CancellationToken cancellationToken)
    {
        var set = await Aca(
            ["sandbox", "lifecycle", "set", "--id", sandbox, "--auto-suspend", "disable"],
            cancellationToken
        );

        if (set.ExitCode != 0)
        {
            throw new AgentProcessHostException(
                "Auto-suspend could not be disabled for this Run's sandbox, and the platform's "
                    + "default suspends a sandbox whose agent is thinking. Refusing rather than "
                    + $"running a Run that may stop halfway. ({Detail(set)})"
            );
        }
    }

    /// <summary>
    /// Deny-default egress is <b>opt-in, not the default</b>: measured 2026-08-08, a sandbox with
    /// no policy reached <c>example.com</c> and <c>pypi.org</c> with 200s while <c>egress show</c>
    /// reported none configured — whatever the platform's documentation says about denying by
    /// default. So the habitat's policy is applied before the agent starts, or the Run refuses.
    /// </summary>
    async Task ApplyEgress(string sandbox, CancellationToken cancellationToken)
    {
        string[] rules =
        [
            .. options.EgressAllow.SelectMany(host => new[] { "--rule", $"{host}:Allow" }),
        ];

        var set = await Aca(
            ["sandbox", "egress", "set", "--id", sandbox, "--default", "Deny", .. rules],
            cancellationToken
        );

        if (set.ExitCode != 0)
        {
            throw new AgentProcessHostException(
                "The egress policy could not be applied to this Run's sandbox. A sandbox with no "
                    + "policy has unrestricted outbound access, so the Run refuses rather than "
                    + $"executing an agent that can reach anything. ({Detail(set)})"
            );
        }
    }

    // ---- The workspace, sent rather than mounted ----

    async Task SendWorkspace(
        string sandbox,
        string workingDirectory,
        CancellationToken cancellationToken
    )
    {
        var sent = await Aca(
            [
                "sandbox",
                "fs",
                "cp",
                "--id",
                sandbox,
                "--source",
                workingDirectory,
                "--dest",
                workingDirectory,
            ],
            cancellationToken
        );

        if (sent.ExitCode != 0)
        {
            throw new AgentProcessHostException(
                $"The Run's workspace could not be sent to its sandbox. ({Detail(sent)})"
            );
        }
    }

    // ---- Previews (design D5) ----

    /// <summary>
    /// Created <b>without</b> <c>--anonymous</c>, so the platform leaves it behind Entra and the
    /// portal stays the only door. Handing out the sandbox's own public URL would move the
    /// preview's boundary outside the product and make "nothing after the Run" depend on a
    /// deletion happening.
    /// </summary>
    async Task PublishPreview(
        string sandbox,
        RunPreview preview,
        CancellationToken cancellationToken
    )
    {
        var added = await Aca(
            [
                "sandbox",
                "port",
                "add",
                "--id",
                sandbox,
                "--port",
                preview.SandboxPort.ToString(CultureInfo.InvariantCulture),
            ],
            cancellationToken
        );

        if (added.ExitCode != 0)
        {
            // A preview is a convenience, never the Run: a port that cannot be published leaves
            // the read answering "nothing serving yet" rather than failing the work.
            AcaLog.PreviewNotPublished(logger, sandbox, Detail(added));
            return;
        }

        previews.Published(preview.RunId, preview.SandboxPort);
    }

    // ---- Lifecycle ----

    /// <summary>
    /// The Project's own SandboxGroup (design D4). The configured name is a template: where it
    /// contains <c>{project}</c> the Project's id fills it, so one setting describes a deployment
    /// whose groups are per Project rather than requiring one key per Project.
    /// <para>
    /// A Run with no Project — there is no such thing today, and the readiness probe's own
    /// sandboxes are not Runs — falls back to the template as written, which is what a habitat
    /// that never templated it meant anyway.
    /// </para>
    /// </summary>
    string GroupFor(Guid? projectId) =>
        projectId is { } id
            ? options.SandboxGroup.Replace(
                "{project}",
                id.ToString("N"),
                StringComparison.OrdinalIgnoreCase
            )
            : options.SandboxGroup.Replace(
                "{project}",
                "shared",
                StringComparison.OrdinalIgnoreCase
            );

    async Task<string> Create(string group, CancellationToken cancellationToken)
    {
        var created = await Aca(
            ["sandbox", "create", "--group", group, "--disk", options.Disk, "-o", "json"],
            cancellationToken
        );

        if (created.ExitCode != 0)
        {
            throw new AgentProcessHostException(
                $"The sandbox for this Run could not be created. ({Detail(created)})"
            );
        }

        var id = SandboxId(created.Stdout);
        if (id is null)
        {
            throw new AgentProcessHostException(
                "The sandbox was created but its id could not be read from the response, so it "
                    + "can neither be used nor cleaned up. Refusing rather than leaking it."
            );
        }

        AcaLog.Created(logger, id);
        return id;
    }

    /// <summary>
    /// Disposal survives cancellation, like every other launcher here: an abandoned sandbox is the
    /// leak, and it costs money as well as attention.
    /// </summary>
    async Task Dispose(string sandbox)
    {
        try
        {
            await Aca(["sandbox", "delete", "--id", sandbox, "--yes"], CancellationToken.None);
            AcaLog.Disposed(logger, sandbox);
        }
        catch (Exception exception)
        {
            AcaLog.NotDisposed(logger, sandbox, exception.Message);
        }
    }

    // ---- Readiness (design D6 of #279) ----

    public async Task<AgentHostReadiness> CheckReadiness(CancellationToken cancellationToken)
    {
        try
        {
            var doctor = await Aca(["sandbox", "list", "-o", "json"], cancellationToken);

            return doctor.ExitCode == 0
                ? new AgentHostReadiness(
                    Ready: true,
                    Where: $"a per-Run sandbox in {options.SandboxGroup}",
                    Remedy: null
                )
                : new AgentHostReadiness(
                    Ready: false,
                    Where: $"a per-Run sandbox in {options.SandboxGroup}",
                    Remedy: "The sandbox group could not be reached. Check that this deployment's "
                        + "identity still holds the Container Apps SandboxGroup Data Owner role on "
                        + $"'{options.SandboxGroup}' — a newly granted one takes about a minute to "
                        + $"propagate. ({Detail(doctor)})"
                );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new AgentHostReadiness(
                Ready: false,
                Where: $"a per-Run sandbox in {options.SandboxGroup}",
                Remedy: $"The sandbox platform could not be asked whether it is ready: {exception.Message}"
            );
        }
    }

    /// <summary>
    /// Asked inside a sandbox, because that is the machine a Run depends on (#279 design D6). The
    /// answer is a property of the disk image, so it is cached on its own cadence exactly as the
    /// sbx host's is — creating a microVM to ask twice would spend seconds on a fact that has not
    /// moved.
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

    async Task<bool> AskInASandbox(string command, CancellationToken cancellationToken)
    {
        string? sandbox = null;
        try
        {
            sandbox = await Create(GroupFor(projectId: null), cancellationToken);
            var version = await Aca(
                ["sandbox", "exec", "--id", sandbox, "-c", $"{command} --version"],
                cancellationToken
            );
            return version.ExitCode == 0;
        }
        catch (AgentProcessHostException)
        {
            // CheckReadiness already says the host itself is the problem, with its remedy.
            // Reporting the CLI as absent too would print two sentences for one fault.
            return false;
        }
        finally
        {
            if (sandbox is not null)
            {
                await Dispose(sandbox);
            }
        }
    }

    /// <summary>
    /// The models a runtime offers, asked inside a sandbox for the same reason the CLI check is
    /// (#291 design D2): the list is a property of the disk image and of the credentials the group
    /// holds, not of the process asking. Cached on the same cadence, because each ask is a microVM.
    /// <para>
    /// Unlike the sbx host there is no carried session to key the cache on — a remote sandbox has
    /// no machine owner — so the command alone identifies the answer.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<string>?> ListModels(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken
    )
    {
        if (
            _models.TryGetValue(command, out var cached)
            && !cached.IsStale(options.CliProbeInterval)
        )
        {
            return cached.Models;
        }

        string? sandbox = null;
        try
        {
            sandbox = await Create(GroupFor(projectId: null), cancellationToken);
            var listed = await Aca(
                ["sandbox", "exec", "--id", sandbox, "-c", Argv(command, arguments)],
                cancellationToken
            );

            // A refusal is "could not ask", never "no models" — the distinction #291 exists to
            // keep, and a failure here is not cached: it is a state of this moment.
            if (listed.ExitCode != 0)
            {
                return null;
            }

            var models = AgentModelListing.Parse(listed.Stdout);
            _models[command] = new ModelVerdict(models, DateTimeOffset.UtcNow);
            return models;
        }
        catch (AgentProcessHostException)
        {
            return null;
        }
        finally
        {
            if (sandbox is not null)
            {
                await Dispose(sandbox);
            }
        }
    }

    readonly System.Collections.Concurrent.ConcurrentDictionary<string, ModelVerdict> _models = new(
        StringComparer.Ordinal
    );

    sealed record ModelVerdict(IReadOnlyList<string> Models, DateTimeOffset At)
    {
        public bool IsStale(TimeSpan after) => DateTimeOffset.UtcNow - At > after;
    }

    readonly System.Collections.Concurrent.ConcurrentDictionary<string, CliVerdict> _cliAnswers =
        new(StringComparer.Ordinal);

    sealed record CliVerdict(bool Answered, DateTimeOffset At)
    {
        public bool IsStale(TimeSpan after) => DateTimeOffset.UtcNow - At > after;
    }

    // ---- The CLI ----

    /// <summary>
    /// Shelled out to rather than called through a client library, because there is no .NET SDK:
    /// searched 2026-08-08, <c>SandboxGroup</c> and <c>sandbox</c> in any path each return zero
    /// results across <c>Azure/azure-sdk-for-net</c>'s default branch, against 20 in the Python SDK
    /// and 38 in the JavaScript one. This is the same shape the sbx host already runs.
    /// </summary>
    async Task<AgentProcessOutcome> Aca(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await HeadlessProcess.Run(
                options.CommandPath,
                arguments,
                Environment.CurrentDirectory,
                new Dictionary<string, string>(),
                options.CallTimeout,
                cancellationToken
            );
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new AgentProcessHostException(
                $"This habitat executes agents in Azure sandboxes ({AgentSandboxComposition.LauncherKey}"
                    + $" = {AgentSandboxComposition.AcaLauncher}), but the aca CLI could not be "
                    + $"started at '{options.CommandPath}'. Install it "
                    + "(curl -fsSL https://aka.ms/aca-cli-install | sh), or name its path in "
                    + $"'{AgentSandboxComposition.CommandPathKey}'. ({exception.Message})"
            );
        }
    }

    /// <summary>The sandbox id out of a `create -o json` response, without taking a JSON dependency
    /// on a preview surface whose shape is expected to move.</summary>
    internal static string? SandboxId(string stdout)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            stdout,
            "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"
        );

        return match.Success ? match.Value : null;
    }

    static string Argv(string fileName, IReadOnlyList<string> arguments) =>
        string.Join(' ', new[] { fileName }.Concat(arguments).Select(Quote));

    /// <summary>Single-quoted for the shell inside the sandbox; an embedded quote is closed,
    /// escaped and reopened, which is the only form that survives every value.</summary>
    static string Quote(string value) =>
        $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";

    static string Detail(AgentProcessOutcome outcome) =>
        string.IsNullOrWhiteSpace(outcome.Stderr)
            ? $"exit {outcome.ExitCode}"
            : $"exit {outcome.ExitCode}: {outcome.Stderr.Trim()}";
}

static partial class AcaLog
{
    [LoggerMessage(
        EventId = 6260,
        Level = LogLevel.Information,
        Message = "Sandbox {Sandbox} created"
    )]
    public static partial void Created(ILogger logger, string sandbox);

    [LoggerMessage(
        EventId = 6261,
        Level = LogLevel.Information,
        Message = "Sandbox {Sandbox} disposed"
    )]
    public static partial void Disposed(ILogger logger, string sandbox);

    [LoggerMessage(
        EventId = 6262,
        Level = LogLevel.Error,
        Message = "Sandbox {Sandbox} could not be disposed and may still be running: {Detail}"
    )]
    public static partial void NotDisposed(ILogger logger, string sandbox, string detail);

    [LoggerMessage(
        EventId = 6263,
        Level = LogLevel.Warning,
        Message = "Sandbox {Sandbox} could not publish its preview port: {Detail}"
    )]
    public static partial void PreviewNotPublished(ILogger logger, string sandbox, string detail);
}

/// <summary>
/// What a habitat declares to execute Runs in Azure sandboxes (#296). Two of these are corrections
/// to platform defaults rather than preferences — see <see cref="AcaAgentProcessHost"/> — and the
/// composition refuses a habitat that leaves them unsaid, because a deployment that forgets is a
/// deployment whose agent runs unrestricted.
/// </summary>
sealed class AcaSandboxOptions
{
    public const string DefaultCommand = "aca";

    /// <summary>
    /// The public prebuilt disk carrying Claude Code — measured 2026-08-08: Ubuntu 26.04 with
    /// `claude` 2.1.198 as a native binary and git 2.55.0, so a deployment needs no image of ours.
    /// </summary>
    public const string DefaultDisk = "claude";

    /// <summary>
    /// GitHub, because every Run's agent does git work. Everything else a habitat needs is its own
    /// to declare: a deny-default policy is only as good as the list it is paired with.
    /// </summary>
    public static readonly string[] DefaultEgressAllow = ["github.com", "api.github.com"];

    public required string CommandPath { get; init; }

    /// <summary>The Project's own group (design D4), so a Run bills as its own Project (#244).</summary>
    public required string SandboxGroup { get; init; }

    public required string Disk { get; init; }

    public required IReadOnlyList<string> EgressAllow { get; init; }

    /// <summary>
    /// How often the poll loop asks a sandbox what its agent has written. Frequent enough that a
    /// Member watching sees it work (UC-027), sparse enough not to spend an exec per second across
    /// the thirty minutes BR-005 allows.
    /// </summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// One CLI call, not one Run. Comfortably above the ~1 s an exec round-trips and comfortably
    /// below the ceiling measured at 50–60 s, which this host never approaches because it polls.
    /// </summary>
    public TimeSpan CallTimeout { get; init; } = TimeSpan.FromSeconds(45);

    /// <summary>
    /// A property of the disk image rather than of the moment, so it is asked rarely — creating a
    /// microVM to re-answer it would spend seconds on a fact that has not moved.
    /// </summary>
    public TimeSpan CliProbeInterval { get; init; } = TimeSpan.FromMinutes(15);
}
