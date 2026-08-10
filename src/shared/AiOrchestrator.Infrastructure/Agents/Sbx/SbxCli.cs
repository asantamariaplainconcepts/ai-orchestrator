namespace AiOrchestrator.ServiceDefaults.Agents.Sbx;

/// <summary>
/// The one seam every sbx collaborator runs a command through: shells out to the sbx binary,
/// and turns "the binary is not there at all" into a remedy rather than a raw ENOENT (#279).
/// </summary>
sealed class SbxCli(SbxSandboxOptions options)
{
    /// <summary>Bounded so a hung CLI cannot hold a Run open; long enough for a cold daemon.</summary>
    public static readonly TimeSpan Brief = TimeSpan.FromSeconds(30);

    public async Task<AgentProcessOutcome> Run(
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

    public static string Detail(AgentProcessOutcome outcome) =>
        outcome.TimedOut ? "the sbx CLI did not answer in time"
        : string.IsNullOrWhiteSpace(outcome.Stderr) ? $"exit {outcome.ExitCode}"
        : $"exit {outcome.ExitCode}: {Truncate(outcome.Stderr)}";

    static string Truncate(string text) =>
        text.Length <= 500 ? text.Trim() : text[..500].Trim() + " …(truncated)";
}
