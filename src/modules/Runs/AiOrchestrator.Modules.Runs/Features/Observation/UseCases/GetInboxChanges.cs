using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Projects.Contracts;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Features.Observation.UseCases;

/// <summary>
/// inbox-open-prs — the open changes waiting for a review, beside the Runs waiting on a human.
/// Its own endpoint rather than a wider <see cref="GetInbox.Entry"/>, deliberately (design D1):
/// the shell's badge computes <c>length</c> over the inbox array and polls it from every page,
/// and a change is not a Run wait — folding them together would inflate a count that means
/// "Runs waiting on you" and put a per-project vendor read on a 30-second cadence.
/// <para>
/// Cross-project like the inbox itself, and scoped the same way. Each project degrades alone: a
/// failing Connector contributes its reason, never a blank group (design's per-project rule).
/// </para>
/// </summary>
sealed class GetInboxChanges : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/inbox/changes",
                async (ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(GetInboxChanges))
            .WithTags("Runs");

    [Requires(Access.FiltersToCaller)]
    internal sealed record Query : IQuery<Response>;

    /// <summary>
    /// Entries across every visible project, newest first, plus the projects whose vendor read
    /// was refused — reported apart, so one bad Connector never blanks the group (BR-008 stays
    /// visible: the list is the vendor's answer, including the answer "no").
    /// </summary>
    internal sealed record Response(IReadOnlyList<Entry> Changes, IReadOnlyList<Refusal> Refusals);

    /// <summary>
    /// <paramref name="RunId"/> is set when the change's URL matches a Run's recorded output
    /// link — the product's own work, joined from what the Runs already store rather than asked
    /// of the vendor (design D4).
    /// </summary>
    internal sealed record Entry(
        Guid ProjectId,
        string? ProjectName,
        int Number,
        string Title,
        string Url,
        DateTimeOffset CreatedAt,
        Guid? RunId
    );

    internal sealed record Refusal(Guid ProjectId, string? ProjectName, string Reason);

    internal sealed class Handler(
        RunsDbContext database,
        IChangeReader changes,
        IProjectCatalog projects,
        IProjectPermissions permissions
    ) : IAppQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            // Scoped like the inbox (#13): entries carry titles from other people's repositories,
            // so an unfiltered list would leak exactly what project visibility exists to fence.
            // Null means ALL of them — the owner and the self-host habitat — and unlike the inbox,
            // which filters a Runs query, this surface asks each project a question, so "all"
            // needs an actual list: the catalogue's, active projects only.
            var visible = await permissions.VisibleProjects(cancellationToken);
            var scope =
                visible?.ToList()
                ?? await projects.ActiveProjectIds(cancellationToken) as IReadOnlyList<Guid>;

            var entries = new List<Entry>();
            var refusals = new List<Refusal>();

            foreach (var projectId in scope)
            {
                var open = await changes.Open(projectId, cancellationToken);
                if (open.Reason is null && open.Changes.Count == 0)
                {
                    // Nothing to say for this project — no Connector, or simply no open changes.
                    continue;
                }

                var projectName = await projects.Name(projectId, cancellationToken);

                if (open.Reason is not null)
                {
                    refusals.Add(new Refusal(projectId, projectName, open.Reason));
                    continue;
                }

                // The product's own changes, recognised from what the Runs already store —
                // never asked of the vendor. Matching on OutputLink shipped dead (#274's defect,
                // fixed by run-on-a-pr): the publish step that wrote it was retired (DEC-062)
                // and no Run has carried one since. What the ceremony does own is the branch:
                // a `run/<id>` head branch carries its Run's id, and a change-targeted Run
                // records the change it updates.
                var ceremonyRunIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
                foreach (var change in open.Changes)
                {
                    const string ceremonyPrefix = "run/";
                    if (
                        change.HeadBranch.StartsWith(ceremonyPrefix, StringComparison.Ordinal)
                        && Guid.TryParse(
                            change.HeadBranch.AsSpan(ceremonyPrefix.Length),
                            out var parsed
                        )
                    )
                    {
                        ceremonyRunIds[change.HeadBranch] = parsed;
                    }
                }

                var candidateIds = ceremonyRunIds.Values.ToList();
                var numbers = open.Changes.Select(change => change.Number).ToList();
                var owned = await database
                    .Runs.Where(run =>
                        run.ProjectId == projectId
                        && (
                            candidateIds.Contains(run.Id)
                            || (
                                run.TargetChangeNumber != null
                                && numbers.Contains(run.TargetChangeNumber.Value)
                            )
                        )
                    )
                    .Select(run => new { run.Id, run.TargetChangeNumber })
                    .ToListAsync(cancellationToken);

                // A branch id only counts once the Run is confirmed to exist in this project —
                // a branch that merely looks like the ceremony's must not claim a Run it hasn't.
                var confirmed = owned.Select(run => run.Id).ToHashSet();
                var byChangeNumber = owned
                    .Where(run => run.TargetChangeNumber is not null)
                    .GroupBy(run => run.TargetChangeNumber!.Value)
                    .ToDictionary(group => group.Key, group => group.First().Id);

                entries.AddRange(
                    open.Changes.Select(change => new Entry(
                        projectId,
                        projectName,
                        change.Number,
                        change.Title,
                        change.Url,
                        change.CreatedAt,
                        ceremonyRunIds.TryGetValue(change.HeadBranch, out var branchRun)
                            && confirmed.Contains(branchRun)
                                ? branchRun
                            : byChangeNumber.TryGetValue(change.Number, out var targetRun)
                                ? targetRun
                            : null
                    ))
                );
            }

            return new Response([.. entries.OrderByDescending(entry => entry.CreatedAt)], refusals);
        }
    }
}
