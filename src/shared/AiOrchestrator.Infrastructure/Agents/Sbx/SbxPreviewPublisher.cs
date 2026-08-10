using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents.Sbx;

/// <summary>Reads back the host port sbx allocated for a Run's preview and records it.</summary>
sealed class SbxPreviewPublisher(SbxCli cli, RunPreviewHost previews, ILogger logger)
{
    /// <summary>
    /// Reads back the port sbx actually allocated and records it. A preview that cannot be
    /// resolved is NOT a Run failure: the agent's work is the Run, and a missing window is a
    /// missing window. It is logged and the Run proceeds without one, which the read then
    /// reports as no preview — the honest answer.
    /// </summary>
    public async Task Record(
        string sandbox,
        BuildingBlocks.Agents.RunPreview preview,
        CancellationToken cancellationToken
    )
    {
        var listed = await cli.Run(["ports", sandbox], SbxCli.Brief, cancellationToken);

        if (listed.ExitCode != 0 || HostPort(listed.Stdout, preview.SandboxPort) is not { } port)
        {
            SandboxLog.PreviewUnavailable(logger, sandbox, SbxCli.Detail(listed));
            return;
        }

        previews.Published(preview.RunId, port);
        SandboxLog.PreviewPublished(logger, sandbox, port);
    }

    /// <summary>
    /// `sbx ports` prints a table: HOST IP, HOST PORT, SANDBOX PORT, PROTOCOL — and lists the
    /// same mapping once per address family (127.0.0.1 and ::1), so the first row for our sandbox
    /// port is the answer and the rest are the same answer again.
    /// </summary>
    static int? HostPort(string stdout, int sandboxPort)
    {
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var columns = line.Split(
                (char[])[' ', '\t'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );

            if (
                columns.Length >= 3
                && int.TryParse(columns[1], out var host)
                && int.TryParse(columns[2], out var inside)
                && inside == sandboxPort
            )
            {
                return host;
            }
        }

        return null;
    }
}
