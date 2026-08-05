using Docker.DotNet;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Dispatch;

/// <summary>
/// Asks docker how the pod host is doing, on the cadence the panel promises (design review 5c).
/// Before this, docker's absence was discovered only by launching — the Run sat Queued and the
/// explanation lived in a log nobody was reading. The probe turns that silence into a state the
/// panel and the environment chip can render, with the last-checked time beside it.
/// <para>
/// One question with a three-way answer, straight off the socket (#257): an inspected image
/// proves both the daemon and the image; the daemon answering "no such image" proves the daemon
/// alone — and their remedies differ (a down daemon is started, a missing image is pulled or
/// built), which is the reason <c>AgentPodsSnapshot.ImagePresent</c> is three-valued.
/// </para>
/// </summary>
public sealed class AgentPodsProbe(
    AgentPodsHost host,
    PodLaunchOptions options,
    ILogger<AgentPodsProbe> logger
) : BackgroundService
{
    /// <summary>
    /// Generous for a local socket call, but a docker daemon mid-wedge can hang instead of
    /// refuse — and a probe that hangs forever reports nothing, which is the exact silence it
    /// exists to end.
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
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);

        try
        {
            using var docker = DockerSocket.CreateClient();
            // One socket call answers both when it succeeds: an inspected image implies a
            // reachable daemon.
            await docker.Images.InspectImageAsync(options.Image, timeout.Token);
            return (DockerReady: true, ImagePresent: true);
        }
        catch (DockerImageNotFoundException)
        {
            // The daemon itself composed that answer — reachable, image absent.
            return (DockerReady: true, ImagePresent: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown, not a probe verdict.
            throw;
        }
        catch (Exception)
        {
            // Unreachable, refused, or wedged past the timeout — the panel's first move is the
            // same for all three: look at the daemon.
            return (DockerReady: false, ImagePresent: null);
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
