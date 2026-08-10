namespace AiOrchestrator.ServiceDefaults.Agents.Sbx;

/// <summary>
/// Whether a runtime's CLI answers, and which models it offers, both asked <b>inside a
/// sandbox</b> — the only machine a Run depends on (design D6 / #291 design D2) — and both
/// cached on the image's own cadence rather than the readiness panel's, because creating a
/// sandbox costs seconds and these answers are properties of the template image.
/// </summary>
sealed class SbxCliProbe(
    SbxSandboxOptions options,
    SbxCli cli,
    SbxSandboxLifecycle lifecycle,
    SbxSessionCarriage sessionCarriage
)
{
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

    /// <summary>
    /// The models a runtime offers <b>inside a sandbox</b> (#291, design D2), which is the only
    /// answer a Run depends on.
    /// <para>
    /// Cached, because each ask costs a whole microVM — but NOT on <see cref="CliAnswers"/>'
    /// reasoning (design D3). That cache is justified by its answer being a property of the
    /// template image, which does not move. A model list is a property of the image <b>and of the
    /// session this habitat carries in</b>: the `github-copilot/*` entries exist because #288
    /// copied a seat. So the key includes a fingerprint of the carried session, and a developer
    /// who re-authenticates stops being served the models of the seat they left.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<string>?> ListModels(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken
    )
    {
        var key = $"{command}\u0000{sessionCarriage.Fingerprint()}";

        if (_models.TryGetValue(key, out var cached) && !cached.IsStale(options.CliProbeInterval))
        {
            return cached.Models;
        }

        var listed = await AskInASandbox(
            command,
            [command, .. arguments],
            AgentModelListing.Parse,
            cancellationToken
        );

        // A failed ask is not cached: "could not ask" is a state of this moment, and caching it
        // would keep a chooser empty for the probe interval after the machine came back.
        if (listed is not null)
        {
            _models[key] = new ModelVerdict(listed, DateTimeOffset.UtcNow);
        }

        return listed;
    }

    readonly System.Collections.Concurrent.ConcurrentDictionary<string, ModelVerdict> _models = new(
        StringComparer.Ordinal
    );

    sealed record ModelVerdict(IReadOnlyList<string> Models, DateTimeOffset At)
    {
        public bool IsStale(TimeSpan after) => DateTimeOffset.UtcNow - At > after;
    }

    async Task<bool> AskInASandbox(string command, CancellationToken cancellationToken)
    {
        var sandbox = $"aio-probe-{Guid.NewGuid():N}"[..24];

        try
        {
            await lifecycle.Create(
                sandbox,
                Path.GetTempPath(),
                command,
                preview: null,
                cancellationToken
            );
        }
        catch (AgentProcessHostException)
        {
            // The host itself is the problem, and CheckReadiness already says so with its
            // remedy. Reporting the CLI as absent too would print two sentences for one fault.
            return false;
        }

        try
        {
            var version = await cli.Run(
                ["exec", sandbox, command, "--version"],
                SbxCli.Brief,
                cancellationToken
            );
            return !version.TimedOut && version.ExitCode == 0;
        }
        finally
        {
            await lifecycle.Dispose(sandbox);
        }
    }

    /// <summary>
    /// Runs one command inside a throwaway sandbox and reads its stdout, or null where the ask
    /// itself failed. The sibling of <see cref="AskInASandbox(string, CancellationToken)"/> —
    /// same create-and-dispose, different question.
    /// </summary>
    async Task<T?> AskInASandbox<T>(
        string template,
        IReadOnlyList<string> argv,
        Func<string, T> read,
        CancellationToken cancellationToken
    )
        where T : class
    {
        var sandbox = $"aio-probe-{Guid.NewGuid():N}"[..24];

        try
        {
            await lifecycle.Create(
                sandbox,
                Path.GetTempPath(),
                template,
                preview: null,
                cancellationToken
            );
        }
        catch (AgentProcessHostException)
        {
            // The host itself is the problem, and CheckReadiness already says so with its
            // remedy. "Could not ask" is the honest answer here.
            return null;
        }

        try
        {
            var answered = await cli.Run(
                ["exec", sandbox, .. argv],
                SbxCli.Brief,
                cancellationToken
            );
            return answered.TimedOut || answered.ExitCode != 0 ? null : read(answered.Stdout);
        }
        finally
        {
            await lifecycle.Dispose(sandbox);
        }
    }
}
