using System.Diagnostics;
using System.Text;
using AiOrchestrator.BuildingBlocks.Agents;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// Git via the CLI against a folder the owner already has (#210). No clone, no token, no push:
/// the run branch is created in place, the Agent works with whatever credentials the host's own
/// tooling holds, and the branch itself is the output. The one mutation this class makes to the
/// owner's checkout — switching branches — is undone on every failure path.
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
        var isRepo = await Git(path, ["rev-parse", "--is-inside-work-tree"], cancellationToken);
        if (isRepo.ExitCode != 0)
        {
            return new PathInspection(true, false, null, null);
        }

        var branch = await Git(path, ["branch", "--show-current"], cancellationToken);
        var status = await Git(path, ["status", "--porcelain"], cancellationToken);

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

        // BR-016, re-checked where the race ends: the dispatch check passed moments ago, but
        // the folder belongs to a person who may have typed since.
        if (inspection.IsClean is not true)
        {
            return LocalWorkspaceErrors.DirtyTree(path);
        }

        // The way back: a branch name when on one, the bare commit otherwise (detached HEAD).
        var previous =
            inspection.Branch
            ?? (await Git(path, ["rev-parse", "HEAD"], cancellationToken)).Stdout.Trim();

        var create = await Git(path, ["switch", "-c", branch], cancellationToken);
        if (create.ExitCode != 0)
        {
            return LocalWorkspaceErrors.BranchFailed(create.Stderr.Trim());
        }

        return new LocalWorkspace(path, branch, previous);
    }

    public async Task<ErrorOr<bool>> Conclude(
        LocalWorkspace workspace,
        string commitMessage,
        bool succeeded,
        CancellationToken cancellationToken = default
    )
    {
        if (!succeeded)
        {
            // The owner finds their folder as they left it — the run branch stays for forensics,
            // but their checkout comes back.
            await Git(workspace.Path, ["switch", workspace.PreviousRef], cancellationToken);
            return false;
        }

        var status = await Git(workspace.Path, ["status", "--porcelain"], cancellationToken);
        if (string.IsNullOrWhiteSpace(status.Stdout))
        {
            // Nothing uncommitted — either the Agent committed as it went (its commits are on
            // the branch) or it changed nothing. The branch stays checked out either way: it is
            // the recorded output, and deciding it is empty is the reader's call, not ours.
            return false;
        }

        await Git(workspace.Path, ["add", "--all"], cancellationToken);
        var commit = await Git(
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

    static async Task<(int ExitCode, string Stdout, string Stderr)> Git(
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
