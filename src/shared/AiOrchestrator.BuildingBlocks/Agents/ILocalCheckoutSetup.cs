namespace AiOrchestrator.BuildingBlocks.Agents;

/// <summary>
/// Runs the Admin-configured command that makes a Local Run's fresh checkout buildable, before the
/// Agent starts (#332). A checkout created for one Run has no installed dependencies and no build
/// outputs, so an Agent asked to make the tests pass meets a tree where they cannot run.
/// <para>
/// <b>Its own seam, deliberately.</b> Not <c>IAgentProcessHost</c>: that is one composed singleton
/// per habitat — sbx or ACA where those launchers are configured — and routing setup through it
/// would send an Admin's install command into a sandbox that has no such checkout. Setup runs on the
/// machine that owns the folder, which is this process, always. Not
/// <see cref="ILocalCodeWorkspace"/> either: that seam is git, with a fixed argument list and no
/// user input anywhere in it, and executing an operator's arbitrary command line is a different
/// responsibility with a different test surface (design D3).
/// </para>
/// </summary>
public interface ILocalCheckoutSetup
{
    /// <summary>
    /// Runs <paramref name="commandLine"/> to completion in <paramref name="workingDirectory"/>,
    /// bounded by <paramref name="budget"/> — which is what remains of the phase's timeout, never a
    /// limit of its own (BR-005, design D4).
    /// <para>
    /// The line is handed to a shell rather than parsed, because what an Admin needs to write is
    /// <c>pnpm install &amp;&amp; pnpm build</c>. The exit status is therefore the shell's own: a
    /// line whose last command succeeds reports success whatever an earlier one did, and nothing
    /// here reinterprets the line to decide otherwise.
    /// </para>
    /// </summary>
    /// <param name="onOutput">
    /// Receives both streams line by line as they arrive, so a setup that hangs is legible while it
    /// hangs (UC-027) rather than only once it ends.
    /// </param>
    Task<LocalSetupOutcome> Run(
        string commandLine,
        string workingDirectory,
        TimeSpan budget,
        Action<string> onOutput,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// How the setup command ended. <paramref name="TimedOut"/> is BR-005's own outcome and is kept
/// distinct from any exit code, because a Run that ran out of time did not fail its build and its
/// reason must not claim it did.
/// </summary>
/// <param name="Output">
/// Both streams, interleaved as they arrived. The Run's log already has every line (BR-014); this
/// exists so a refusal can carry the tail as evidence.
/// </param>
public sealed record LocalSetupOutcome(bool TimedOut, int ExitCode, string Output)
{
    public bool Succeeded => !TimedOut && ExitCode == 0;
}

/// <summary>
/// Stage-named refusals, the <see cref="LocalWorkspaceErrors"/> pattern. Both sentences say
/// <b>setup</b> in as many words: a person reading <c>Failed</c> needs to know whether to fix their
/// repository's build or their Story, and a reason that could be either is the failure these exist
/// to prevent (#332, design D5).
/// </summary>
public static class LocalSetupErrors
{
    /// <summary>
    /// The command ran and refused. Carries the command as configured <b>and</b> the tail of its
    /// output — the tail, because that is where a build error is, and because the whole output is
    /// already in the Run's log (BR-014), so the reason carries evidence rather than a transcript.
    /// Nothing retries (BR-004): whoever reads this is the retry.
    /// </summary>
    public static ErrorOr.Error Failed(string commandLine, string outputTail) =>
        ErrorOr.Error.Failure(
            "LocalSetup.Failed",
            $"The setup command failed before the agent started. Command: '{commandLine}'. "
                + $"Its output ended with:\n{outputTail}"
        );

    /// <summary>
    /// The budget went while setup held it. Names <b>the limit</b> rather than the command, because
    /// a Run that ran out of time did not fail its build — BR-005's own sentence, reached through a
    /// different door.
    /// </summary>
    public static ErrorOr.Error TimedOut(string commandLine, TimeSpan limit) =>
        ErrorOr.Error.Failure(
            "LocalSetup.TimedOut",
            $"The run exceeded its {limit.TotalMinutes:0.##}-minute phase timeout while the setup "
                + $"command was still running, and was stopped. Command: '{commandLine}'."
        );

    /// <summary>
    /// The budget was already gone when setup finished, so the agent was never invoked. The same
    /// limit, named the same way — what differs is only which step was holding the clock.
    /// </summary>
    public static ErrorOr.Error BudgetExhausted(TimeSpan limit) =>
        ErrorOr.Error.Failure(
            "LocalSetup.BudgetExhausted",
            $"The run exceeded its {limit.TotalMinutes:0.##}-minute phase timeout preparing its "
                + "checkout, leaving no time for the agent, which was not started."
        );
}
