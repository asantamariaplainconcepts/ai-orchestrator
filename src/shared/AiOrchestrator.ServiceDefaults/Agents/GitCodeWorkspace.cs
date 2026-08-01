using System.Diagnostics;
using System.Text;
using AiOrchestrator.BuildingBlocks.Agents;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Octokit;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// Git via the CLI, the pull request via Octokit — both confined here (design D2). The
/// credential rides an in-memory remote URL per invocation and is never written to any config
/// that survives the job (BR-010): clone and push name the URL on the command line of a child
/// process and nowhere else.
/// </summary>
public sealed class GitCodeWorkspace : ICodeWorkspace
{
    public Task<ErrorOr<PreparedWorkspace>> Prepare(
        CodeCoordinates coordinates,
        Guid runId,
        string token,
        CancellationToken cancellationToken
    ) => Prepare(coordinates, $"run/{runId}", token, cancellationToken);

    public async Task<ErrorOr<PreparedWorkspace>> Prepare(
        CodeCoordinates coordinates,
        string branch,
        string token,
        CancellationToken cancellationToken
    )
    {
        var path = Directory.CreateTempSubdirectory("workspace-").FullName;

        var clone = await Git(
            null,
            ["clone", "--depth", "1", RemoteUrl(coordinates, token), path],
            cancellationToken
        );
        if (clone.ExitCode != 0)
        {
            return WorkspaceErrors.CloneFailed(Sanitise(clone.Stderr, token));
        }

        var checkout = await Git(path, ["checkout", "-b", branch], cancellationToken);
        if (checkout.ExitCode != 0)
        {
            return WorkspaceErrors.CloneFailed(Sanitise(checkout.Stderr, token));
        }

        return new PreparedWorkspace(coordinates, path, branch);
    }

    public async Task<ErrorOr<PublishedChange>> Publish(
        PreparedWorkspace workspace,
        string title,
        string body,
        string token,
        CancellationToken cancellationToken,
        bool draft = false
    )
    {
        var status = await Git(workspace.Path, ["status", "--porcelain"], cancellationToken);
        if (string.IsNullOrWhiteSpace(status.Stdout))
        {
            // The honesty gate (design D3): an empty PR pretends work happened.
            return WorkspaceErrors.NoChanges();
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
                title,
            ],
            cancellationToken
        );
        if (commit.ExitCode != 0)
        {
            return WorkspaceErrors.PushFailed(Sanitise(commit.Stderr, token));
        }

        // The credential exists on this one command line and in no stored remote (BR-010).
        var push = await Git(
            workspace.Path,
            ["push", RemoteUrl(workspace.Coordinates, token), $"HEAD:{workspace.Branch}"],
            cancellationToken
        );
        if (push.ExitCode != 0)
        {
            return WorkspaceErrors.PushFailed(Sanitise(push.Stderr, token));
        }

        try
        {
            var client = new GitHubClient(new ProductHeaderValue("ai-orchestrator"))
            {
                Credentials = new Credentials(token),
            };
            var repository = await client.Repository.Get(
                workspace.Coordinates.Owner,
                workspace.Coordinates.Repository
            );
            var pullRequest = await client.PullRequest.Create(
                workspace.Coordinates.Owner,
                workspace.Coordinates.Repository,
                new NewPullRequest(title, workspace.Branch, repository.DefaultBranch)
                {
                    Body = body,
                    Draft = draft,
                }
            );

            return new PublishedChange(pullRequest.HtmlUrl);
        }
        catch (ApiException exception)
        {
            return WorkspaceErrors.PullRequestFailed(exception.Message);
        }
    }

    static string RemoteUrl(CodeCoordinates coordinates, string token) =>
        $"https://x-access-token:{token}@github.com/{coordinates.Owner}/{coordinates.Repository}.git";

    /// <summary>Belt and braces: the token never reaches a stored reason even via git's own echo.</summary>
    static string Sanitise(string text, string token) =>
        text.Replace(token, "***", StringComparison.Ordinal);

    static async Task<(int ExitCode, string Stdout, string Stderr)> Git(
        string? workingDirectory,
        string[] arguments,
        CancellationToken cancellationToken
    )
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory ?? Path.GetTempPath(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        // Never prompt: a job has no terminal, and a hang here would run out BR-005's clock
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

public static class CodeWorkspaceComposition
{
    public static TBuilder AddCodeWorkspace<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<ICodeWorkspace, GitCodeWorkspace>();
        return builder;
    }
}
