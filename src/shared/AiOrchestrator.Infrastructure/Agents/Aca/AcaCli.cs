namespace AiOrchestrator.ServiceDefaults.Agents.Aca;

/// <summary>
/// The one seam every aca collaborator runs a command through.
/// <para>
/// Shelled out to rather than called through a client library, because there is no .NET SDK:
/// searched 2026-08-08, <c>SandboxGroup</c> and <c>sandbox</c> in any path each return zero
/// results across <c>Azure/azure-sdk-for-net</c>'s default branch, against 20 in the Python SDK
/// and 38 in the JavaScript one. This is the same shape the sbx host already runs.
/// </para>
/// </summary>
sealed class AcaCli(AcaSandboxOptions options)
{
    public async Task<AgentProcessOutcome> Run(
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

    public static string Argv(string fileName, IReadOnlyList<string> arguments) =>
        string.Join(' ', new[] { fileName }.Concat(arguments).Select(Quote));

    /// <summary>Single-quoted for the shell inside the sandbox; an embedded quote is closed,
    /// escaped and reopened, which is the only form that survives every value.</summary>
    public static string Quote(string value) =>
        $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";

    /// <summary>
    /// An authorization refusal, told apart from every other failure by what the CLI prints —
    /// there is no exit code that distinguishes them, and treating "not authorised yet" the same
    /// as "that disk does not exist" would make one wait a minute for nothing and the other fail
    /// instantly for something temporary.
    /// </summary>
    public static bool IsAuthorization(AgentProcessOutcome outcome) =>
        outcome.Stderr.Contains("403", StringComparison.Ordinal)
        || outcome.Stderr.Contains("Forbidden", StringComparison.OrdinalIgnoreCase)
        || outcome.Stderr.Contains("AuthorizationFailed", StringComparison.OrdinalIgnoreCase);

    public static string Detail(AgentProcessOutcome outcome) =>
        string.IsNullOrWhiteSpace(outcome.Stderr)
            ? $"exit {outcome.ExitCode}"
            : $"exit {outcome.ExitCode}: {outcome.Stderr.Trim()}";
}
