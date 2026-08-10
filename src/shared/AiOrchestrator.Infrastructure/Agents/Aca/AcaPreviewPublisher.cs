using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents.Aca;

sealed class AcaPreviewPublisher(AcaCli cli, RunPreviewHost previews, ILogger logger)
{
    /// <summary>
    /// Created <b>without</b> <c>--anonymous</c>, so the platform leaves it behind Entra and the
    /// portal stays the only door. Handing out the sandbox's own public URL would move the
    /// preview's boundary outside the product and make "nothing after the Run" depend on a
    /// deletion happening.
    /// </summary>
    public async Task Publish(
        string sandbox,
        BuildingBlocks.Agents.RunPreview preview,
        CancellationToken cancellationToken
    )
    {
        var added = await cli.Run(
            [
                "sandbox",
                "port",
                "add",
                "--id",
                sandbox,
                "--port",
                preview.SandboxPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ],
            cancellationToken
        );

        if (added.ExitCode != 0)
        {
            // A preview is a convenience, never the Run: a port that cannot be published leaves
            // the read answering "nothing serving yet" rather than failing the work.
            AcaLog.PreviewNotPublished(logger, sandbox, AcaCli.Detail(added));
            return;
        }

        previews.Published(preview.RunId, preview.SandboxPort);
    }
}
