using AiOrchestrator.Modules.Backlog.Contracts;

namespace AiOrchestrator.Modules.Projects.Features.Automations;

/// <summary>
/// Finds the pipeline a repository already has (#229).
/// <para>
/// The reason this exists is `ds-connect`: it carries `.claude/commands/ds/` with grill, propose,
/// implement, refine, sync and status — the same steps this product seeds, written for that team.
/// Installing a second copy under `ai/prompts/` would overwrite a team's conventions with the
/// product's opinion of them, which is exactly what DEC-048 refused for the grill's rubric.
/// </para>
/// <para>
/// It <b>proposes</b> and never picks (design D1): a heuristic that reconfigured a project the
/// first time somebody pressed a button would be wrong for somebody, and the only thing worse
/// than not finding a pipeline is adopting the wrong one.
/// </para>
/// </summary>
sealed class PipelineDiscovery(IDocumentReader documents)
{
    /// <summary>
    /// Conventional locations, in order (design D2). The Connector's own setting first — an
    /// explicit answer beats a guess — then the product's convention, then where agent tooling
    /// keeps commands. One subdirectory deep and no further: `ds/` is one level, and unbounded
    /// recursion turns a form action into a repository crawl.
    /// </summary>
    internal static readonly string[] ConventionalRoots = ["ai/prompts", ".claude/commands"];

    public async Task<IReadOnlyList<DirectoryListing>> Candidates(
        Guid projectId,
        string? configuredDirectory,
        CancellationToken cancellationToken
    )
    {
        var found = new List<DirectoryListing>();

        foreach (var root in Roots(configuredDirectory))
        {
            var listing = await documents.ListPromptFiles(projectId, root, cancellationToken);

            // A refusal travels too: "the vendor would not let me look" is a different answer
            // from "there is nothing here", and only the caller can say which one to show.
            if (listing.Files.Count > 0 || listing.Failure is not null)
            {
                found.Add(listing);
            }

            // One level down, but only for a root that exists: probing subdirectories of a
            // directory that is not there would be several reads to learn what one already said.
            if (listing.Absent)
            {
                continue;
            }

            foreach (var child in listing.Subdirectories)
            {
                var nested = await documents.ListPromptFiles(
                    projectId,
                    $"{root}/{child}",
                    cancellationToken
                );

                if (nested.Files.Count > 0)
                {
                    found.Add(nested);
                }
            }
        }

        return found;
    }

    /// <summary>
    /// The candidate roots this search will read, in order — exposed so the surface can say where
    /// it looked when it found nothing, which is the difference between "no pipeline here" and
    /// "this button does not work".
    /// </summary>
    internal static IReadOnlyList<string> Roots(string? configuredDirectory)
    {
        var roots = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            roots.Add(configuredDirectory.Trim().Trim('/'));
        }

        roots.AddRange(
            ConventionalRoots.Where(root => !roots.Contains(root, StringComparer.Ordinal))
        );
        return roots;
    }
}
