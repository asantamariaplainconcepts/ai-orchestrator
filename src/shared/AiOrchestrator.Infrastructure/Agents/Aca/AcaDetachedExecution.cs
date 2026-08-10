using System.Globalization;

namespace AiOrchestrator.ServiceDefaults.Agents.Aca;

/// <summary>
/// Runs the agent detached inside a sandbox and polls its log — the decision design D2 turns on.
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
sealed class AcaDetachedExecution(AcaSandboxOptions options, AcaCli cli)
{
    public async Task<AgentProcessOutcome> RunDetachedAndPoll(
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
            $"cd {AcaCli.Quote(workingDirectory)} && nohup sh -c {AcaCli.Quote($"{AcaCli.Argv(fileName, arguments)} > {log} 2>&1; echo $? > {status}")} > /dev/null 2>&1 &";

        var started = await cli.Run(
            ["sandbox", "exec", "--id", sandbox, "-c", command],
            cancellationToken
        );
        if (started.ExitCode != 0)
        {
            throw new AgentProcessHostException(
                $"The agent could not be started inside the sandbox. ({AcaCli.Detail(started)})"
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
                Forward(tail, onOutput, ref forwarded, ended: true);
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
                Forward(last, onOutput, ref forwarded, ended: true);

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
        var read = await cli.Run(
            ["sandbox", "exec", "--id", sandbox, "-c", $"tail -n +{skip + 1} {path} 2>/dev/null"],
            cancellationToken
        );

        return read.ExitCode == 0 ? read.Stdout : string.Empty;
    }

    /// <summary>
    /// Forwards the lines of a chunk, holding back the last one while the agent is still writing.
    /// <para>
    /// <paramref name="ended"/> is what makes the last line arrive at all. The hold-back exists so
    /// a watcher never sees half a sentence — a chunk not ending in a newline is a line still
    /// being written — but that reasoning stops the moment the exit code is on disk: nothing is
    /// partial after the process has gone. Without this flag the final line of every Run was
    /// dropped, which is invisible against a stand-in that answers instantly and was measured
    /// against real Azure on 2026-08-09 (task 7.2).
    /// </para>
    /// </summary>
    static void Forward(
        string chunk,
        Action<string>? onOutput,
        ref int forwarded,
        bool ended = false
    )
    {
        if (onOutput is null || chunk.Length == 0)
        {
            return;
        }

        var lines = chunk.Split('\n');
        var complete = ended ? lines.Length : lines.Length - 1;

        for (var index = 0; index < complete; index++)
        {
            // A trailing newline leaves an empty final element; forwarding it would put a blank
            // line at the end of every Run's log.
            if (ended && index == lines.Length - 1 && lines[index].Length == 0)
            {
                break;
            }

            onOutput(lines[index]);
            forwarded++;
        }
    }
}
