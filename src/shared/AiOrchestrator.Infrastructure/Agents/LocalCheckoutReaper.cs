using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// Removes the Local-Run checkouts a previous process abandoned, once, at startup (#331, design D5).
/// <para>
/// <b>Why a <c>finally</c> is not enough — measured, and already on record for the sibling
/// substrate.</b> <see cref="Sbx.SbxSandboxLifecycle"/> exists because a developer's machine held 31
/// running sandboxes and 125 GB of disk: every creation there was already paired with a disposal in a
/// <c>finally</c>, and that pairing was correct. What it could not survive was the process not being
/// there to run it. A checkout leaks identically — stop <c>aspire run</c> mid-Run and the worktree
/// outlives the only reference anyone had to it.
/// </para>
/// <para>
/// <b>It never removes a branch.</b> The branch is the Run's output and outlives its checkout by
/// design (BR-016); both <c>worktree remove</c> and <c>worktree prune</c> leave it intact, measured.
/// Nothing in this class runs <c>branch -D</c>, and a test asserts it stays that way.
/// </para>
/// </summary>
sealed class LocalCheckoutReaper(ILogger<LocalCheckoutReaper> logger, string? root = null)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Startup must not fail because a sweep could not read a directory. The disk cost of a
        // missed sweep is the thing this class is for; refusing to start is strictly worse.
        try
        {
            await Sweep(cancellationToken);
        }
        catch (Exception exception)
        {
            LocalCheckoutLog.SweepFailed(logger, exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    async Task Sweep(CancellationToken cancellationToken)
    {
        var abandoned = LocalCheckoutRoster.Abandoned(root);
        if (abandoned.Count == 0)
        {
            return;
        }

        // Distinct, because many checkouts of one folder share a repository and pruning it twice
        // buys nothing.
        var repositories = new HashSet<string>(StringComparer.Ordinal);

        foreach (var checkout in abandoned)
        {
            var repository = RepositoryOf(checkout);
            if (repository is not null)
            {
                repositories.Add(repository);

                // Ask git first: it removes the directory *and* the record together, which is the
                // only path that leaves nothing behind in either place.
                var removed = await LocalFolderWorkspace.RunGit(
                    repository,
                    ["worktree", "remove", "--force", checkout],
                    cancellationToken
                );
                if (removed.ExitCode == 0)
                {
                    continue;
                }
            }

            // No repository to ask, or git declined — the directory is still ours and still waste.
            // The record it may leave behind is what the prune below is for.
            try
            {
                if (Directory.Exists(checkout))
                {
                    Directory.Delete(checkout, recursive: true);
                }
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException)
            {
                LocalCheckoutLog.CheckoutSurvived(logger, checkout, exception);
            }
        }

        foreach (var repository in repositories)
        {
            // Reconciles the repository's record with a disk that no longer has these worktrees.
            // Idempotent, and safe in either order with the removals above (measured).
            await LocalFolderWorkspace.RunGit(repository, ["worktree", "prune"], cancellationToken);
        }

        LocalCheckoutLog.Reaped(logger, abandoned.Count);
    }

    /// <summary>
    /// The repository a checkout belongs to, read from the <c>.git</c> file git writes into every
    /// worktree (<c>gitdir: /path/to/repo/.git/worktrees/&lt;name&gt;</c>). Null when the file is
    /// missing or does not have that shape — a directory in our namespace that is not a worktree is
    /// still ours to delete, just not ours to prune.
    /// </summary>
    static string? RepositoryOf(string checkout)
    {
        var pointer = Path.Combine(checkout, ".git");
        string content;
        try
        {
            if (!File.Exists(pointer))
            {
                return null;
            }
            content = File.ReadAllText(pointer).Trim();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        const string Marker = "gitdir:";
        if (!content.StartsWith(Marker, StringComparison.Ordinal))
        {
            return null;
        }

        // …/<repo>/.git/worktrees/<name> → …/<repo>. Walk up rather than parse: the segment names
        // are git's, and three parents is exactly what its own layout puts between them.
        var gitDir = content[Marker.Length..].Trim();
        var worktreesDirectory = Directory.GetParent(gitDir);
        var dotGit = worktreesDirectory?.Parent;
        var repository = dotGit?.Parent;

        return worktreesDirectory?.Name == "worktrees" && dotGit?.Name == ".git"
            ? repository?.FullName
            : null;
    }
}

static partial class LocalCheckoutLog
{
    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Information,
        Message = "Reaped {Count} abandoned local run checkout(s); their branches were left alone."
    )]
    public static partial void Reaped(ILogger logger, int count);

    [LoggerMessage(
        EventId = 4102,
        Level = LogLevel.Warning,
        Message = "An abandoned local run checkout at {Checkout} could not be removed."
    )]
    public static partial void CheckoutSurvived(ILogger logger, string checkout, Exception error);

    [LoggerMessage(
        EventId = 4103,
        Level = LogLevel.Warning,
        Message = "The abandoned-checkout sweep did not complete; startup continues."
    )]
    public static partial void SweepFailed(ILogger logger, Exception error);
}

public static class LocalCheckoutReaperComposition
{
    /// <summary>
    /// Registers the startup sweep. <b>Composed by the Server alone</b>, deliberately: it is the
    /// process that creates checkouts, and a second process of this product sweeping the same temp
    /// directory would reap the Server's live Runs — the in-process live set (design D5) cannot see
    /// across process boundaries. DEC-016 bounds this at one owner and one machine, not one process.
    /// </summary>
    public static TBuilder AddLocalCheckoutReaper<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHostedService<LocalCheckoutReaper>();
        return builder;
    }
}
