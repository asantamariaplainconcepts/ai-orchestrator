using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Dispatch;

/// <summary>
/// Asks docker how the pod host is doing, on the cadence the panel promises (design review 5c).
/// Before this, docker's absence was discovered only by launching — the Run sat Queued and the
/// explanation lived in a log nobody was reading. The probe turns that silence into a state the
/// panel and the environment chip can render, with the last-checked time beside it.
/// <para>
/// Two questions, cheapest first: does the daemon answer, and does the pod image exist. They are
/// separate because their remedies are — a down daemon is started, a missing image is built —
/// and conflating them sends the operator to the wrong fix (the reason
/// <c>AgentPodsSnapshot.ImagePresent</c> is three-valued).
/// </para>
/// </summary>
public sealed class AgentPodsProbe(
    AgentPodsHost host,
    PodLaunchOptions options,
    ILogger<AgentPodsProbe> logger
) : BackgroundService
{
    /// <summary>
    /// Generous for a local CLI call, but a docker daemon mid-wedge can hang instead of refuse —
    /// and a probe that hangs forever reports nothing, which is the exact silence it exists to end.
    /// </summary>
    static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Transitions are logged, states are not: a healthy machine probed every 30 seconds
        // would otherwise write a diary of nothing changing.
        var previous = default((bool DockerReady, bool? ImagePresent)?);

        while (!stoppingToken.IsCancellationRequested)
        {
            var current = await Probe(stoppingToken);
            host.RecordProbe(current.DockerReady, current.ImagePresent);

            if (current != previous)
            {
                switch (current)
                {
                    case { DockerReady: false }:
                        ProbeLog.DockerUnreachable(logger);
                        break;
                    case { ImagePresent: false }:
                        ProbeLog.ImageMissing(logger, options.Image);
                        break;
                    default:
                        ProbeLog.PodsReady(logger, options.Image);
                        break;
                }
            }

            previous = current;

            try
            {
                await Task.Delay(AgentPodsHost.ProbeInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    async Task<(bool DockerReady, bool? ImagePresent)> Probe(CancellationToken cancellationToken)
    {
        // The image question answers both when it succeeds: an inspected image implies a
        // reachable daemon, so the healthy path costs one CLI call, not two.
        if (await Succeeds(["image", "inspect", options.Image], cancellationToken))
        {
            return (DockerReady: true, ImagePresent: true);
        }

        return await Succeeds(["version", "--format", "{{.Server.Version}}"], cancellationToken)
            ? (DockerReady: true, ImagePresent: false)
            : (DockerReady: false, ImagePresent: null);
    }

    /// <summary>
    /// Exit code zero, and nothing else: the probe never parses docker's output, so a CLI
    /// version changing its wording cannot turn a healthy host red.
    /// </summary>
    static async Task<bool> Succeeds(string[] arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo)!;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ProbeTimeout);

            // Drained so a chatty CLI cannot fill a pipe and stall the exit — the same rule the
            // launcher follows, one level smaller.
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The probe's own timeout, not shutdown: a wedged daemon counts as unreachable.
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // Exited between the timeout and the kill — the race's harmless arm.
                }

                return false;
            }

            await Task.WhenAll(stdout, stderr);
            return process.ExitCode == 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The CLI itself is missing or refused to start — the same "not ready" the panel
            // shows for a down daemon, because the operator's first move is identical.
            return false;
        }
    }
}

static partial class ProbeLog
{
    [LoggerMessage(
        EventId = 6230,
        Level = LogLevel.Warning,
        Message = "Agent pods unavailable: the docker daemon is not reachable"
    )]
    public static partial void DockerUnreachable(ILogger logger);

    [LoggerMessage(
        EventId = 6231,
        Level = LogLevel.Warning,
        Message = "Agent pods unavailable: docker answers but the image {Image} is not present"
    )]
    public static partial void ImageMissing(ILogger logger, string image);

    [LoggerMessage(
        EventId = 6232,
        Level = LogLevel.Information,
        Message = "Agent pods ready: docker answers and {Image} is present"
    )]
    public static partial void PodsReady(ILogger logger, string image);
}
