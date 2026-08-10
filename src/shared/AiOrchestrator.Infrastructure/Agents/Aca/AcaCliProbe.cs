namespace AiOrchestrator.ServiceDefaults.Agents.Aca;

/// <summary>
/// Whether a runtime's CLI answers, and which models it offers, both asked inside a sandbox
/// (#279/#291 design D6/D2) — the machine a Run depends on — and cached on the disk image's own
/// cadence, because each ask is a whole sandbox.
/// </summary>
sealed class AcaCliProbe(AcaSandboxOptions options, AcaCli cli, AcaSandboxLifecycle lifecycle)
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

    async Task<bool> AskInASandbox(string command, CancellationToken cancellationToken)
    {
        string? sandbox = null;
        try
        {
            sandbox = await lifecycle.Create(
                lifecycle.GroupFor(projectId: null),
                cancellationToken
            );
            var version = await cli.Run(
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
                await lifecycle.Dispose(sandbox);
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
            sandbox = await lifecycle.Create(
                lifecycle.GroupFor(projectId: null),
                cancellationToken
            );
            var listed = await cli.Run(
                ["sandbox", "exec", "--id", sandbox, "-c", AcaCli.Argv(command, arguments)],
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
                await lifecycle.Dispose(sandbox);
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
}
