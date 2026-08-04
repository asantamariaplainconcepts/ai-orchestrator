using System.Diagnostics;
using AiOrchestrator.BuildingBlocks.Dispatch;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Dispatch;

/// <summary>
/// #246 — each Run in its own container: the ACA Job pattern shrunk to one machine. The image is
/// the DispatchWorker's, started in its per-Run entry mode (<c>--run &lt;id&gt;</c>), so the code
/// that executes here is byte-for-byte the code the queue habitat runs.
/// <para>
/// It talks to the docker <b>CLI</b>, which reaches whatever socket the operator mounted — the
/// grant is the operator's, made in their own compose, never this product's default (design D3).
/// Every refusal names what is missing, and there is deliberately no fallback to in-process
/// execution: a fallback would erase the isolation the operator asked for without telling them.
/// </para>
/// </summary>
public sealed class PodRunLauncher(
    PodLaunchOptions options,
    AgentPodsHost pods,
    ILogger<PodRunLauncher> logger
) : IDispatchedRunHandler
{
    /// <summary>
    /// The host bound (design D6): BR-001 bounds per-Story, this bounds the machine. A semaphore
    /// rather than a rejection, because a Run past the cap is delayed, never dropped — the outbox
    /// consumer simply holds the message while the wait lasts.
    /// </summary>
    readonly SemaphoreSlim _slots = new(options.MaxConcurrentPods, options.MaxConcurrentPods);

    public async Task Handle(Guid runId, CancellationToken cancellationToken)
    {
        // Sighted before the slot wait, not after: the Run parked on this semaphore is exactly
        // the one the panel must explain (design review 5b), and it is invisible everywhere else
        // — still Queued in the database, already claimed off the outbox.
        pods.WaitingForSlot(runId);
        try
        {
            await _slots.WaitAsync(cancellationToken);
            try
            {
                pods.Executing(runId);
                DispatchLog.PodStarting(logger, runId, options.Image);
                await Launch(runId, cancellationToken);
            }
            finally
            {
                _slots.Release();
            }
        }
        finally
        {
            pods.Finished(runId);
        }
    }

    async Task Launch(Guid runId, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        startInfo.ArgumentList.Add("run");
        // Removed on exit: the Run's state and log live in the database, and a graveyard of
        // exited containers is a leak, not an audit.
        startInfo.ArgumentList.Add("--rm");

        if (!string.IsNullOrWhiteSpace(options.Network))
        {
            startInfo.ArgumentList.Add("--network");
            startInfo.ArgumentList.Add(options.Network);
        }

        foreach (var (name, value) in options.Environment)
        {
            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add($"{name}={value}");
        }

        foreach (var mount in options.Mounts)
        {
            startInfo.ArgumentList.Add("-v");
            startInfo.ArgumentList.Add(mount);
        }

        startInfo.ArgumentList.Add(options.Image);
        startInfo.ArgumentList.Add("--run");
        startInfo.ArgumentList.Add(runId.ToString());

        Process process;
        try
        {
            process = Process.Start(startInfo)!;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The docker CLI itself is missing — the sentence names the grant that is absent.
            throw new InvalidOperationException(
                "This habitat is configured to execute Runs in pods "
                    + $"(Dispatch:PodImage = {options.Image}), but the docker CLI could not be "
                    + "started. Grant the container the docker socket and CLI, or remove "
                    + "Dispatch:PodImage to execute in-process.",
                exception
            );
        }

        // Drained concurrently with the wait: a pod that fills either pipe would deadlock a
        // reader that only started after exit.
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            // Non-zero is "execution could not happen" (design D4) — no socket, no image, no
            // database. The Run stays where it was; BR-004 forbids anything from retrying, so
            // the failure must carry everything a person needs.
            throw new InvalidOperationException(
                $"The pod for run {runId} exited {process.ExitCode} without executing. "
                    + $"stderr: {Truncate(await stderr)} stdout: {Truncate(await stdout)}"
            );
        }

        DispatchLog.PodFinished(logger, runId);
    }

    static string Truncate(string text) =>
        text.Length <= 2000 ? text : text[..2000] + " …(truncated)";
}

/// <summary>
/// Everything the launcher passes into a pod, decided in composition — the launcher itself knows
/// how to start a container, never what a habitat's database or secret store looks like.
/// </summary>
public sealed class PodLaunchOptions
{
    public required string Image { get; init; }

    /// <summary>Empty omits <c>--network</c> — the operator names the compose network.</summary>
    public string? Network { get; init; }

    public IReadOnlyDictionary<string, string> Environment { get; init; } =
        new Dictionary<string, string>();

    /// <summary>docker <c>-v</c> values, verbatim (<c>source:target[:ro]</c>).</summary>
    public IReadOnlyList<string> Mounts { get; init; } = [];

    public int MaxConcurrentPods { get; init; } = 2;
}

static partial class DispatchLog
{
    [LoggerMessage(
        EventId = 6220,
        Level = LogLevel.Information,
        Message = "Starting a pod for run {RunId} from {Image}"
    )]
    public static partial void PodStarting(ILogger logger, Guid runId, string image);

    [LoggerMessage(
        EventId = 6221,
        Level = LogLevel.Information,
        Message = "Pod for run {RunId} exited"
    )]
    public static partial void PodFinished(ILogger logger, Guid runId);
}
