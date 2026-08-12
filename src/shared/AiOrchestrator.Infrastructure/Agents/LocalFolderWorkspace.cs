using System.Diagnostics;
using System.Text;
using AiOrchestrator.BuildingBlocks.Agents;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// Git via the CLI against a folder the owner already has (#210). No clone, no token, no push: the
/// Agent works with whatever credentials the host's own tooling holds, and the run branch itself is
/// the output.
/// <para>
/// <b>The Run works in its own checkout (#331).</b> A <c>git worktree</c> of the configured folder,
/// on the run branch, in the namespace <see cref="LocalCheckoutRoster"/> claims. A worktree shares
/// the repository's refs, so the branch this produces is already in the owner's repository when the
/// checkout goes away — which is what lets BR-016's "the branch is the output" hold with no
/// push-back ceremony, and what made a worktree the choice over a clone (design D1).
/// </para>
/// <para>
/// <b>This class makes no mutation to the owner's folder at all.</b> Not a branch switch, not a
/// checkout — so there is nothing to undo on a failure path, and a dirty tree is no longer anyone's
/// business.
/// </para>
/// </summary>
public sealed class LocalFolderWorkspace : ILocalCodeWorkspace
{
    public async Task<PathInspection> Inspect(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        if (!Directory.Exists(path))
        {
            return new PathInspection(false, false, null, null);
        }

        // `rev-parse` inside the path, not `-C` from elsewhere: the answer must be about this
        // folder even when a parent is itself a repository.
        var isRepo = await RunGit(path, ["rev-parse", "--is-inside-work-tree"], cancellationToken);
        if (isRepo.ExitCode != 0)
        {
            return new PathInspection(true, false, null, null);
        }

        var branch = await RunGit(path, ["branch", "--show-current"], cancellationToken);
        var status = await RunGit(path, ["status", "--porcelain"], cancellationToken);

        return new PathInspection(
            true,
            true,
            branch.Stdout.Trim() is { Length: > 0 } name ? name : null,
            string.IsNullOrWhiteSpace(status.Stdout)
        );
    }

    public async Task<ErrorOr<LocalWorkspace>> Prepare(
        string path,
        string branch,
        CancellationToken cancellationToken = default
    )
    {
        var inspection = await Inspect(path, cancellationToken);
        if (!inspection.IsGitRepository)
        {
            return LocalWorkspaceErrors.NotARepository(path);
        }

        // No clean-tree check, at either site (#331): the worktree below is created from the
        // repository, not from the working tree, so uncommitted work in the folder is untouched
        // and irrelevant. Measured — `worktree add` from a dirty repository succeeds and leaves
        // the folder's changes exactly where they were.
        var checkout = LocalCheckoutRoster.NewCheckout();

        // `-b` here is also what forbids a second concurrent Run for the same Story: git refuses a
        // branch already checked out in another worktree (exit 128, measured). BR-001 already says
        // so; this is a mechanical second guard, and the branch-name coupling is load-bearing.
        var create = await RunGit(
            path,
            ["worktree", "add", checkout, "-b", branch],
            cancellationToken
        );
        if (create.ExitCode != 0)
        {
            return LocalWorkspaceErrors.CheckoutFailed(path, create.Stderr.Trim());
        }

        // Claimed before it is handed out, so a sweep in this process can never mistake a live
        // Run's checkout for one a dead process abandoned.
        LocalCheckoutRoster.Occupy(checkout);
        return new LocalWorkspace(checkout, branch, path);
    }

    public async Task<ErrorOr<bool>> Conclude(
        LocalWorkspace workspace,
        string commitMessage,
        bool succeeded,
        CancellationToken cancellationToken = default
    )
    {
        // The checkout goes away in every terminal state — success, failure, whatever happened.
        // There is nothing to restore: the owner's folder was never entered, and a run branch
        // that reached a commit survives the removal because a worktree shares the repository's
        // refs (measured, design D1).
        try
        {
            if (!succeeded)
            {
                return false;
            }

            var status = await RunGit(workspace.Path, ["status", "--porcelain"], cancellationToken);
            if (string.IsNullOrWhiteSpace(status.Stdout))
            {
                // Nothing uncommitted — either the Agent committed as it went (its commits are on
                // the branch) or it changed nothing. The branch remains either way: it is the
                // recorded output, and deciding it is empty is the reader's call, not ours.
                return false;
            }

            await RunGit(workspace.Path, ["add", "--all"], cancellationToken);
            var commit = await RunGit(
                workspace.Path,
                [
                    "-c",
                    "user.name=AI Orchestrator",
                    "-c",
                    "user.email=agent@ai-orchestrator.invalid",
                    "commit",
                    "-m",
                    commitMessage,
                ],
                cancellationToken
            );

            return commit.ExitCode != 0
                ? LocalWorkspaceErrors.CommitFailed(commit.Stderr.Trim())
                : true;
        }
        finally
        {
            await Remove(workspace);
        }
    }

    /// <summary>
    /// Gives the checkout back. Run from the configured folder, never from inside the checkout —
    /// git refuses to remove the worktree you are standing in. <c>--force</c> because the Agent
    /// leaves uncommitted files behind on every path where the Run failed, and a checkout this
    /// product created is this product's to reclaim; the branch is untouched by either flag.
    /// </summary>
    static async Task Remove(LocalWorkspace workspace)
    {
        // Released first: even if git leaves the directory behind, the startup sweep is now
        // allowed to finish the job rather than skipping it forever as "live".
        LocalCheckoutRoster.Release(workspace.Path);

        // CancellationToken.None, deliberately. This runs in a `finally` on the cancellation path
        // too, and a cancelled Run that leaked its checkout would be the exact 125 GB failure the
        // sweep exists to clean up — paid for again on every cancel.
        await RunGit(
            workspace.Folder,
            ["worktree", "remove", "--force", workspace.Path],
            CancellationToken.None
        );
    }

    /// <summary>
    /// One git invocation, never prompting. Shared with <see cref="LocalCheckoutReaper"/> so the
    /// sweep speaks to git exactly as the workspace does — same environment, same non-interactive
    /// posture, one place to change either.
    /// </summary>
    internal static async Task<(int ExitCode, string Stdout, string Stderr)> RunGit(
        string workingDirectory,
        string[] arguments,
        CancellationToken cancellationToken
    )
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        // Never prompt: a worker has no terminal, and a hang here would run out BR-005's clock
        // on ceremony instead of on the agent.
        process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => stdout.AppendLine(e.Data);
        process.ErrorDataReceived += (_, e) => stderr.AppendLine(e.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}

public static class LocalWorkspaceComposition
{
    public static TBuilder AddLocalCodeWorkspace<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<ILocalCodeWorkspace, LocalFolderWorkspace>();
        return builder;
    }
}
