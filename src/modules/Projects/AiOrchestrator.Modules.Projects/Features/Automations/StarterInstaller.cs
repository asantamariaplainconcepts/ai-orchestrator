using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Secrets;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Projects.Features.Automations.UseCases;
using ErrorOr;

namespace AiOrchestrator.Modules.Projects.Features.Automations;

/// <summary>
/// Writing starter files into a repository as one branch and one draft pull request.
/// <para>
/// Extracted at #229 rather than copied: filling four gaps is four files in one review (design
/// D4), and a second publish path would be a second set of stage-named refusals to keep in step
/// with the first. Both callers — the single install (#214) and the setup action — resolve the
/// credential, prepare, write, publish and clean up here.
/// </para>
/// </summary>
sealed class StarterInstaller(
    IConnectorReader connectors,
    ISecretResolver secrets,
    ICodeWorkspace workspace
)
{
    /// <summary>
    /// One file to write: a repository-relative path and the bytes that go in it.
    /// <para>
    /// <paramref name="OnlyIfAbsent"/> is for a file whose presence this caller has not already
    /// established (#269): a tier's prerequisites land outside the prompt directory, so the gap
    /// computation that vouches for a prompt says nothing about them. Prompts pass false and keep
    /// their existing contract.
    /// </para>
    /// </summary>
    public sealed record File(string Path, string Content, bool OnlyIfAbsent = false);

    /// <summary>
    /// What an install did. <paramref name="PullRequestUrl"/> is null where nothing needed writing —
    /// not a failure (#269): every file being already present is a clean outcome, and the caller that
    /// chose it must not be told its own decision went wrong.
    /// </summary>
    public sealed record InstallOutcome(
        IReadOnlyList<string> Written,
        IReadOnlyList<string> Skipped,
        string? PullRequestUrl
    );

    public async Task<ErrorOr<InstallOutcome>> Install(
        Guid projectId,
        string branch,
        IReadOnlyList<File> files,
        string title,
        string body,
        CancellationToken cancellationToken
    )
    {
        if (files.Count == 0)
        {
            // Being handed nothing at all is a caller bug, and stays a refusal for the same reason
            // the workspace refuses an empty change set: an empty pull request would claim work that
            // did not happen. Distinct from "everything turned out to be present", which is decided
            // below against the clone and is an outcome rather than an error.
            return WorkspaceErrors.NoChanges();
        }

        var connector = await connectors.Find(projectId, cancellationToken);
        if (connector is null)
        {
            return StarterInstallErrors.NoConnector("this project has no Connector");
        }

        string token;
        try
        {
            token = await secrets.Resolve(connector.SecretName, cancellationToken);
        }
        catch (SecretNotFoundException exception)
        {
            return StarterInstallErrors.NoConnector(exception.Message);
        }

        var prepared = await workspace.Prepare(
            new CodeCoordinates(connector.Owner, connector.Repository),
            branch,
            token,
            cancellationToken
        );
        if (prepared.IsError)
        {
            // Stage-named already: WorkspaceErrors.CloneFailed says "clone", not "something".
            return prepared.Errors;
        }

        try
        {
            var written = new List<string>();
            var skipped = new List<string>();

            foreach (var file in files)
            {
                var target = System.IO.Path.Combine(
                    prepared.Value.Path,
                    file.Path.Replace('/', System.IO.Path.DirectorySeparatorChar)
                );

                // Decided against the clone, which *is* the default-branch content this branch comes
                // from — no vendor read, and no window between asking and branching in which the
                // answer could change. An existing file always wins.
                if (file.OnlyIfAbsent && System.IO.File.Exists(target))
                {
                    skipped.Add(file.Path);
                    continue;
                }

                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
                await System.IO.File.WriteAllTextAsync(target, file.Content, cancellationToken);
                written.Add(file.Path);
            }

            if (written.Count == 0)
            {
                // Everything was already there. Publishing would open an empty pull request, and
                // refusing would report a failure for a repository that simply had the files.
                return new InstallOutcome(written, skipped, PullRequestUrl: null);
            }

            var published = await workspace.Publish(
                prepared.Value,
                title,
                body,
                token,
                cancellationToken,
                draft: true
            );

            return published.IsError
                ? published.Errors
                : ErrorOrFactory.From(
                    new InstallOutcome(written, skipped, published.Value.PullRequestUrl)
                );
        }
        finally
        {
            try
            {
                Directory.Delete(prepared.Value.Path, recursive: true);
            }
            catch (IOException)
            {
                // A temp directory that outlives the request is a leak, not a failure.
            }
        }
    }

    /// <summary>`estimate.md` → `estimate`; anything path-ish flattens to one safe segment.</summary>
    public static string BranchSlug(string saveAs)
    {
        var name = saveAs.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? saveAs[..^3]
            : saveAs;
        return string.Join(
            '-',
            name.Split(
                ['/', '\\', ' '],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
        );
    }
}
