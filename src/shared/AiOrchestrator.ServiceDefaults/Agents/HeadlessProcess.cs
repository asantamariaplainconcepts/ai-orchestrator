using System.Diagnostics;
using System.Text;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// The agent CLI as a child of this process: captured streams, environment-only credentials, and
/// BR-005's kill-on-timeout. The default <see cref="IAgentProcessHost"/> and the behaviour every
/// habitat had before sandboxing existed — a host that names no sandbox launcher runs exactly
/// this.
/// </summary>
sealed class LocalAgentProcessHost : IAgentProcessHost
{
    /// <summary>
    /// This host hands the values to the child; it has no way to authenticate on its behalf.
    /// </summary>
    public bool SuppliesCredentials => false;

    public string CredentialSource =>
        "the credentials resolved for this Run, in the agent process's environment";

    public Task<AgentProcessOutcome> Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? onOutput = null
    ) =>
        HeadlessProcess.Run(
            fileName,
            arguments,
            workingDirectory,
            environment,
            timeout,
            cancellationToken,
            onOutput
        );

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
