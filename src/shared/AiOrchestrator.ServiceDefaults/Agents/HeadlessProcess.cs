using System.Diagnostics;
using System.Text;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// The one way an agent CLI is spawned: captured streams, environment-only credentials, and
/// BR-005's kill-on-timeout. Shared by every runtime implementation so the timeout semantics
/// cannot drift between them.
/// </summary>
static class HeadlessProcess
{
    public sealed record Outcome(bool TimedOut, int ExitCode, string Stdout, string Stderr);

    public static async Task<Outcome> Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken
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
        process.OutputDataReceived += (_, e) => stdout.AppendLine(e.Data);
        process.ErrorDataReceived += (_, e) => stderr.AppendLine(e.Data);

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

            return new Outcome(TimedOut: true, ExitCode: -1, stdout.ToString(), stderr.ToString());
        }

        return new Outcome(TimedOut: false, process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
