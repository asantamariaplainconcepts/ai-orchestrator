using AiOrchestrator.BuildingBlocks.Agents;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents.Sbx;

/// <summary>
/// Whether and how the machine owner's agent-CLI session travels into a sandbox (#288), and the
/// carriage itself.
/// </summary>
sealed class SbxSessionCarriage(SbxSandboxOptions options, SbxCli cli, ILogger logger)
{
    /// <summary>The sandbox user's home, where every CLI looks for its credential.</summary>
    const string SandboxHome = "/home/agent";

    /// <summary>Whether this habitat carries any credential file into a sandbox at all (#288).</summary>
    public bool HasCarriedFiles => options.SessionFiles.Count > 0;

    /// <summary>
    /// Why this runtime's session cannot travel, when carriage is on and it cannot (#288). Null
    /// where carriage is off — no promise was made — or where the runtime's credential is a file
    /// the copy reaches.
    /// </summary>
    public SessionCarriageGap? SessionUnavailableFor(
        string runtimeName,
        string command,
        string? credentialSecretName
    )
    {
        if (options.SessionFiles.Count == 0 || !options.KeychainRuntimes.Contains(command))
        {
            return null;
        }

        // The runtime's own configured name where it has one — a remedy that invents a second
        // name would leave the developer with a stored key nothing reads.
        var secretName = credentialSecretName ?? command;

        return new SessionCarriageGap(
            AgentRuntimeRemedies.SessionCannotTravel(
                command,
                "this machine's keychain",
                runtimeName,
                secretName,
                alreadyNamed: credentialSecretName is not null
            ),
            AgentRuntimeRemedies.StoreSandboxSecret(options.CommandPath, secretName)
        );
    }

    /// <summary>
    /// What the carried session currently is, cheaply and without reading a credential's contents
    /// into anything that outlives the call — size and last-write per carried file. Enough to
    /// change when a developer re-authenticates, which is the only change that has to invalidate
    /// the list. Empty where the habitat carries nothing.
    /// </summary>
    public string Fingerprint()
    {
        if (options.SessionFiles.Count == 0)
        {
            return string.Empty;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.Join(
            '|',
            options.SessionFiles.Select(file =>
            {
                var info = new FileInfo(Path.Combine(home, file));
                return info.Exists ? $"{file}:{info.Length}:{info.LastWriteTimeUtc.Ticks}" : file;
            })
        );
    }

    /// <summary>
    /// Copies the machine owner's agent-CLI credentials into the sandbox, where the habitat asked
    /// for it (#288). A copy, never a mount: it lives exactly as long as the sandbox and an agent
    /// cannot write back into the developer's own session state.
    /// <para>
    /// Only the credential FILES travel, not the CLI's configuration tree — observed 2026-08-08:
    /// opencode's entire session is <c>~/.local/share/opencode/auth.json</c> at 950 bytes, while
    /// <c>~/.config/opencode</c> is over a gigabyte of caches that buy nothing. With that one file
    /// copied in, <c>opencode auth list</c> saw both configured providers and a headless run
    /// answered on the owner's GitHub Copilot seat with no API key anywhere.
    /// </para>
    /// <para>
    /// Deliberately silent about what it cannot carry: a credential held in an OS keychain — which
    /// is where Claude Code keeps its on macOS — has no file to copy, and saying so is the
    /// readiness panel's job, not a failure at Run time.
    /// </para>
    /// </summary>
    public async Task Carry(string sandbox, CancellationToken cancellationToken)
    {
        foreach (var file in options.SessionFiles)
        {
            var source = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                file
            );

            if (!File.Exists(source))
            {
                // Not signed into that CLI, or it keeps its credential somewhere a copy cannot
                // reach. Neither is this method's problem to report.
                continue;
            }

            // Staged through a readable copy, because `sbx cp` preserves the HOST's uid and
            // mode — observed 2026-08-08: a 0600 credential owned by uid 501 lands inside the
            // sandbox still 0600 and still owned by 501, so the sandbox user cannot read it and
            // cannot chown it either. `opencode auth list` then reports "0 credentials" from a
            // file that is demonstrably present, which reads as carriage working and failing at
            // the same time.
            //
            // The staging copy is 0644 inside a 0700 directory: readable to the sandbox user
            // once it is inside, and reachable by nobody else on this machine.
            var staging = Directory.CreateTempSubdirectory("aio-carry-");

            try
            {
                var readable = Path.Combine(staging.FullName, Path.GetFileName(file));
                File.Copy(source, readable, overwrite: true);
                if (!OperatingSystem.IsWindows())
                {
                    // The mode is what travels, so it is the mode that has to be widened. On
                    // Windows there are no Unix bits to widen and sbx is not a host anyway.
                    File.SetUnixFileMode(
                        readable,
                        UnixFileMode.UserRead
                            | UnixFileMode.UserWrite
                            | UnixFileMode.GroupRead
                            | UnixFileMode.OtherRead
                    );
                }

                var landing = $"/tmp/{Guid.NewGuid():N}";

                var copied = await cli.Run(
                    ["cp", readable, $"{sandbox}:{landing}"],
                    SbxCli.Brief,
                    cancellationToken
                );

                if (copied.ExitCode != 0)
                {
                    SandboxLog.SessionNotCarried(logger, sandbox, file, SbxCli.Detail(copied));
                    continue;
                }

                // Re-created BY the sandbox user, which is what makes it readable, and returned
                // to 0600 because a CLI's own credential file is not a world-readable thing.
                // The landing copy is deliberately left where it is: removing it would fail for
                // the same ownership reason, and it dies with the sandbox regardless.
                var placed = await cli.Run(
                    [
                        "exec",
                        sandbox,
                        "sh",
                        "-c",
                        $"mkdir -p $(dirname {SandboxHome}/{file}) && rm -f {SandboxHome}/{file} "
                            + $"&& cp {landing} {SandboxHome}/{file} "
                            + $"&& chmod 600 {SandboxHome}/{file}",
                    ],
                    SbxCli.Brief,
                    cancellationToken
                );

                if (placed.ExitCode != 0)
                {
                    SandboxLog.SessionNotCarried(logger, sandbox, file, SbxCli.Detail(placed));
                    continue;
                }

                SandboxLog.SessionCarried(logger, sandbox, file);
            }
            finally
            {
                staging.Delete(recursive: true);
            }
        }
    }
}
