using System.Runtime.InteropServices;
using AiOrchestrator.BuildingBlocks.Agents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// The Admin's setup command, run in the Run's own checkout through the same process discipline the
/// Agent gets (#332). <see cref="HeadlessProcess"/> already streams both streams line by line as
/// they arrive (#96), enforces a timeout with <c>Kill(entireProcessTree: true)</c> — which matters
/// here, because an install spawns children — and reports a timeout as an outcome distinct from any
/// exit code (BR-005). Writing a second one of those would have been a second thing to keep right.
/// </summary>
public sealed class LocalCheckoutSetupRunner : ILocalCheckoutSetup
{
    public async Task<LocalSetupOutcome> Run(
        string commandLine,
        string workingDirectory,
        TimeSpan budget,
        Action<string> onOutput,
        CancellationToken cancellationToken = default
    )
    {
        var (fileName, arguments) = Shell(commandLine);

        var outcome = await HeadlessProcess.Run(
            fileName,
            arguments,
            workingDirectory,
            // Nothing added. The child inherits this process's environment, which is exactly what
            // LocalAgentProcessHost gives the Agent — so setup and the Agent resolve the same PATH
            // and the same toolchain. A dependency that installs for one and is missing for the
            // other is the failure this agreement forecloses (design D2).
            new Dictionary<string, string>(),
            budget,
            cancellationToken,
            onOutput
        );

        // Both streams, in the order they arrived, for the refusal's evidence. The Run's log already
        // has each line; this is what LocalSetupErrors.Failed takes its tail from.
        var output = string.Concat(outcome.Stdout, outcome.Stderr);

        return new LocalSetupOutcome(outcome.TimedOut, outcome.ExitCode, output);
    }

    /// <summary>
    /// The line goes to a shell rather than being parsed into argv, because what an Admin needs to
    /// write is <c>pnpm install &amp;&amp; pnpm build</c> and argv cannot express it.
    /// <para>
    /// <b>Not a login shell.</b> Measured on macOS 2026-08-12: <c>sh -lc</c> sourced the owner's
    /// <c>~/.profile</c> and wrote an error from it into the output <i>before running anything</i> —
    /// a Run's log would carry the operator's profile noise, and a broken profile line would read as
    /// a setup failure. <c>-c</c> inherits this process's environment instead, which is the one the
    /// Agent will run in (design D2).
    /// </para>
    /// </summary>
    static (string FileName, string[] Arguments) Shell(string commandLine) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ("cmd.exe", ["/c", commandLine])
            : ("/bin/sh", ["-c", commandLine]);
}

public static class LocalCheckoutSetupComposition
{
    public static TBuilder AddLocalCheckoutSetup<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<ILocalCheckoutSetup, LocalCheckoutSetupRunner>();
        return builder;
    }
}
