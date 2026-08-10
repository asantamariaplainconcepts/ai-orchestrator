namespace AiOrchestrator.ServiceDefaults.Agents.Aca;

/// <summary>The workspace, sent rather than mounted — the property #296's whole design rests on.</summary>
sealed class AcaWorkspaceTransfer(AcaSandboxOptions options, AcaCli cli)
{
    public async Task Send(
        string sandbox,
        string workingDirectory,
        CancellationToken cancellationToken
    )
    {
        // **Sent as one archive, because the platform has no recursive copy.** Measured
        // 2026-08-09, the first time the shipped host met the real CLI: `fs cp` takes
        // `<SOURCE> <DESTINATION>` with no `--id` (it answered `unexpected argument '--id'`),
        // and handed a directory it answers `Is a directory (os error 21)`. No verb under
        // `sandbox fs` — ls, cat, write, rm, mkdir, stat, cp — copies a tree. A Run's workspace
        // is a git clone, so "send the workspace" had to become tar → copy → untar. The
        // stand-in script accepted every shape, which is exactly the class of defect only the
        // real exercise finds (task 7.2).
        var archive = Path.Combine(Path.GetTempPath(), $"aio-workspace-{Guid.NewGuid():N}.tar.gz");

        try
        {
            var packed = await HeadlessProcess.Run(
                "tar",
                ["-czf", archive, "-C", workingDirectory, "."],
                Environment.CurrentDirectory,
                new Dictionary<string, string>(),
                options.CallTimeout,
                cancellationToken
            );

            if (packed.ExitCode != 0)
            {
                throw new AgentProcessHostException(
                    $"The Run's workspace could not be packed for its sandbox. ({AcaCli.Detail(packed)})"
                );
            }

            var remote = $"/tmp/{Path.GetFileName(archive)}";

            var sent = await cli.Run(
                ["sandbox", "fs", "cp", archive, $"{sandbox}:{remote}"],
                cancellationToken
            );

            if (sent.ExitCode != 0)
            {
                throw new AgentProcessHostException(
                    $"The Run's workspace could not be sent to its sandbox. ({AcaCli.Detail(sent)})"
                );
            }

            var unpacked = await cli.Run(
                [
                    "sandbox",
                    "exec",
                    "--id",
                    sandbox,
                    "-c",
                    $"mkdir -p {AcaCli.Quote(workingDirectory)} "
                        + $"&& tar -xzf {remote} -C {AcaCli.Quote(workingDirectory)} && rm -f {remote}",
                ],
                cancellationToken
            );

            if (unpacked.ExitCode != 0)
            {
                throw new AgentProcessHostException(
                    $"The Run's workspace arrived at its sandbox but could not be unpacked. "
                        + $"({AcaCli.Detail(unpacked)})"
                );
            }
        }
        finally
        {
            File.Delete(archive);
        }
    }
}
