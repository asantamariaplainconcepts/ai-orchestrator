using System.Diagnostics;
using System.Text;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// The agent CLI as a child of this process: captured streams, environment-only credentials, and
/// BR-005's kill-on-timeout. The default <see cref="IAgentProcessHost"/> and the behaviour every
/// habitat had before sandboxing existed — a host that names no sandbox launcher runs exactly
/// this.
/// </summary>
sealed class LocalAgentProcessHost(RunCheckoutHost? checkouts = null) : IAgentProcessHost
{
    /// <summary>
    /// This host hands the values to the child; it has no way to authenticate on its behalf.
    /// </summary>
    public bool SuppliesCredentials => false;

    public string CredentialSource => "carried in the agent process's environment";

    public Task<AgentProcessOutcome> Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? onOutput = null,
        // Ignored, and honestly so: a child process of this one has no port to publish. The
        // preview read reports previews unhosted here, which is a different sentence from a Run
        // having none (run-previews design D2).
        BuildingBlocks.Agents.RunPreview? preview = null,
        Guid? projectId = null,
        // No longer ignored (#358, DEC-070). This used to read "a child of this process has no sandbox
        // to open a shell in", which was true while a terminal required one. A host terminal opens in
        // the Run's own working directory, so this host is the one place that knows both halves of the
        // pairing a terminal needs — and, like the sbx host, it publishes rather than keeping it as a
        // parameter nobody stores.
        Guid? runId = null
    ) =>
        RunAndPublish(
            fileName,
            arguments,
            workingDirectory,
            environment,
            timeout,
            cancellationToken,
            onOutput,
            runId
        );

    async Task<AgentProcessOutcome> RunAndPublish(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? onOutput,
        Guid? runId
    )
    {
        // Published before the agent starts and removed in `finally`, so "this Run has a terminal" is
        // true exactly while its agent is running. Null where no ledger is registered — a habitat that
        // hosts no terminal keeps the behaviour it had, rather than paying for a feature it refuses.
        var published = runId is not null && checkouts is not null;
        if (published)
        {
            checkouts!.Created(runId!.Value, workingDirectory);
        }

        try
        {
            return await HeadlessProcess.Run(
                fileName,
                arguments,
                workingDirectory,
                environment,
                timeout,
                cancellationToken,
                onOutput
            );
        }
        finally
        {
            if (published)
            {
                checkouts!.Gone(runId!.Value);
            }
        }
    }

    /// <summary>Nothing of its own to be missing: the CLI check is the whole question here.</summary>
    public Task<AgentHostReadiness> CheckReadiness(CancellationToken cancellationToken) =>
        Task.FromResult(AgentHostReadiness.Local);

    public async Task<bool> CliAnswers(string command, CancellationToken cancellationToken)
    {
        try
        {
            // Exit code only — parsing output would let a CLI's wording turn a healthy host red.
            var outcome = await HeadlessProcess.Run(
                command,
                ["--version"],
                Path.GetTempPath(),
                new Dictionary<string, string>(),
                ProbeTimeout,
                cancellationToken
            );
            return !outcome.TimedOut && outcome.ExitCode == 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Missing, not executable, or refusing to start — one verdict, because the
            // operator's first move is identical: install the CLI where this process runs.
            return false;
        }
    }

    /// <summary>The question does not arise: an agent here is a child of this process.</summary>
    public SessionCarriageGap? SessionUnavailableFor(
        string runtimeName,
        string command,
        string? credentialSecretName
    ) => null;

    /// <summary>
    /// Asked of this process, which IS where agents run here. No caching: a local CLI answers in
    /// milliseconds, and the reason the sandbox host caches — that each ask costs a microVM —
    /// simply does not apply.
    /// </summary>
    public async Task<IReadOnlyList<string>?> ListModels(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var outcome = await HeadlessProcess.Run(
                command,
                arguments,
                Path.GetTempPath(),
                new Dictionary<string, string>(),
                ProbeTimeout,
                cancellationToken
            );

            // A non-zero exit is "could not ask", not "no models": a CLI that refused the
            // question has told us nothing about its models (design D6).
            return outcome.TimedOut || outcome.ExitCode != 0
                ? null
                : AgentModelListing.Parse(outcome.Stdout);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Generous for a local <c>--version</c>, but a wedged machine can hang instead of refuse —
    /// and a probe that hangs forever reports nothing, which is the silence it exists to end.
    /// </summary>
    internal static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);
}

/// <summary>
/// The one way an agent CLI is spawned locally: captured streams, environment-only credentials,
/// and BR-005's kill-on-timeout. Kept as a function because a sandbox host reuses none of it —
/// what it shares with them is <see cref="IAgentProcessHost"/>, not this implementation.
/// </summary>
static class HeadlessProcess
{
    public static async Task<AgentProcessOutcome> Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? onOutput = null
    )
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        // Values live in this child's environment for its lifetime and nowhere else (BR-010).
        foreach (var (key, value) in environment)
        {
            process.StartInfo.Environment[key] = value;
        }

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            stdout.AppendLine(e.Data);
            // Forward the line as it arrives (#96). Null lines are the stream closing, not
            // output; the watcher gets exactly what the transcript gets.
            if (e.Data is not null)
            {
                onOutput?.Invoke(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            stderr.AppendLine(e.Data);
            if (e.Data is not null)
            {
                onOutput?.Invoke(e.Data);
            }
        };

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited between the timeout and the kill.
            }

            return new AgentProcessOutcome(
                TimedOut: true,
                ExitCode: -1,
                stdout.ToString(),
                stderr.ToString()
            );
        }

        return new AgentProcessOutcome(
            TimedOut: false,
            process.ExitCode,
            stdout.ToString(),
            stderr.ToString()
        );
    }
}
