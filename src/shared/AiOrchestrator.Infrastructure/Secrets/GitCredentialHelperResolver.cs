using System.Diagnostics;
using System.Text;
using AiOrchestrator.BuildingBlocks.Secrets;

namespace AiOrchestrator.ServiceDefaults.Secrets;

/// <summary>
/// Asks this machine's git credential helper who it is for a vendor host — <c>git credential
/// fill</c>, per read (DEC-069 / ADR-0028).
/// <para>
/// <b>Non-interactive by construction, not by hope.</b> Three things are done to the child's
/// environment rather than one, because each closes a different way for git to wait on a human:
/// <c>GIT_TERMINAL_PROMPT=0</c> forbids the terminal prompt, and <c>GIT_ASKPASS</c> /
/// <c>SSH_ASKPASS</c> are removed so no inherited GUI asker is launched instead. A bounded wait is
/// the backstop for a helper that ignores all three. Any of them firing is a failure carrying its
/// reason — never a wait, because the caller may be the polling cycle (UC-009).
/// </para>
/// </summary>
sealed class GitCredentialHelperResolver(TimeProvider clock) : IHostCredentialResolver
{
    /// <summary>
    /// How long the helper gets. Generous for a keychain read and far below any phase budget: the
    /// point is that a helper waiting for a human is detected as such, not that a slow one is
    /// punished.
    /// </summary>
    internal static readonly TimeSpan Budget = TimeSpan.FromSeconds(10);

    public async Task<HostCredential> Resolve(
        string credentialHost,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialHost);

        using var bounded = new CancellationTokenSource(Budget, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            bounded.Token
        );

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        process.StartInfo.ArgumentList.Add("credential");
        process.StartInfo.ArgumentList.Add("fill");
        process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        process.StartInfo.Environment.Remove("GIT_ASKPASS");
        process.StartInfo.Environment.Remove("SSH_ASKPASS");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => stdout.AppendLine(e.Data);
        process.ErrorDataReceived += (_, e) => stderr.AppendLine(e.Data);

        try
        {
            process.Start();
        }
        catch (Exception exception)
        {
            // git itself is missing. Distinguished from "the helper said no" because the fixes are
            // nothing alike.
            throw new HostCredentialUnavailableException(
                credentialHost,
                $"git could not be started ({exception.Message})"
            );
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // The request half of the credential protocol: the blank line ends it, and closing stdin
        // is what tells git nothing more is coming.
        await process.StandardInput.WriteAsync($"protocol=https\nhost={credentialHost}\n\n");
        await process.StandardInput.FlushAsync(linked.Token);
        process.StandardInput.Close();

        try
        {
            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (bounded.IsCancellationRequested)
        {
            Kill(process);

            throw new HostCredentialUnavailableException(
                credentialHost,
                $"the helper did not answer within {Budget.TotalSeconds:0} seconds, which is what "
                    + "waiting for a person looks like from here"
            );
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }

        if (process.ExitCode != 0)
        {
            // Not logged here as well: the reason travels in the exception to the operator who can
            // act on it, and a warning beside it would only duplicate that at a second severity.
            throw new HostCredentialUnavailableException(
                credentialHost,
                Describe(stderr.ToString())
            );
        }

        var answered = Parse(stdout.ToString());

        // No password is a refusal wearing a success exit code. Returning what we have would put an
        // empty credential on the wire, and the vendor's "unauthorized" would name the wrong cause.
        return string.IsNullOrEmpty(answered.Password)
            ? throw new HostCredentialUnavailableException(
                credentialHost,
                "it answered without a password, so this machine holds no credential for that host"
            )
            : answered;
    }

    static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // It exited between the check and the kill. Nothing to do, and nothing worth saying.
        }
    }

    /// <summary>
    /// The response half: <c>key=value</c> lines. Only the two that matter are read; a helper is
    /// free to send more, and a parser that refused unknown keys would break on that freedom.
    /// </summary>
    static HostCredential Parse(string output)
    {
        string? username = null;
        string? password = null;

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("username=", StringComparison.Ordinal))
            {
                username = trimmed["username=".Length..];
            }
            else if (trimmed.StartsWith("password=", StringComparison.Ordinal))
            {
                password = trimmed["password=".Length..];
            }
        }

        return new HostCredential(password ?? string.Empty, username);
    }

    /// <summary>
    /// git's own words where it gave any. The reason travels to an operator, and BR-004 does not
    /// retry — whoever reads it is the retry, so "it failed" would waste the only attempt.
    /// </summary>
    static string Describe(string stderr)
    {
        var said = stderr
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();

        return string.IsNullOrWhiteSpace(said) ? "it exited without saying why" : said;
    }
}
