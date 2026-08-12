using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Projects.Contracts;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Features.Observation.UseCases;

/// <summary>
/// UC-033 (#335) — what every visible project has in flight, in one read, for the shell's projects
/// tree. A third observation surface beside the per-project list and the Inbox, and the one that
/// answers "what is this project doing" rather than "what waits on me".
/// <para>
/// <b>Read models only</b> — the Runs tables and the Postgres Mirror. That is the constraint, not an
/// implementation detail: this is polled from every portal page, and
/// <see cref="GetInboxChanges"/> exists as a separate endpoint precisely because a per-project
/// <i>vendor</i> read must not sit on a shell cadence. Held Stories cost nothing here because the
/// mirror is local.
/// </para>
/// <para>
/// Deliberately <b>not</b> folded into <see cref="GetInbox"/>: the shell's ambient count is
/// <c>length</c> over that array, so an entry that is not a Run waiting on a human would corrupt a
/// count UC-026 defines.
/// </para>
/// </summary>
sealed class GetInFlight : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/in-flight",
                async (ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(GetInFlight))
            .WithTags("Runs");

    [Requires(Access.FiltersToCaller)]
    internal sealed record Query : IQuery<Response>;

    /// <summary>
    /// Only projects with live work appear. A project with nothing in flight is absent rather than
    /// present-and-empty, so the tree has no empty group to render and no zero to explain.
    /// </summary>
    internal sealed record Response(IReadOnlyList<ProjectEntry> Projects);

    internal sealed record ProjectEntry(
        Guid ProjectId,
        string? ProjectName,
        IReadOnlyList<WorkEntry> Work
    );

    /// <summary>
    /// One node under a project: the subject the Runs belong to, with those Runs nested.
    /// <para>
    /// A Run targets <i>exactly</i> one of a Story or an open change — never both, never neither
    /// (<see cref="Run"/>) — so a node is identified by whichever it has, the same null-per-kind
    /// shape <see cref="GetInbox.Entry"/> already uses. A bare Run row is not renderable: without
    /// its subject it answers "which #491?" with silence.
    /// </para>
    /// <paramref name="Held"/> is the hold (BR-007, DEC-067) and is a fact about the Story, not
    /// about a Run: it is true for a held Story with no Run at all, which is the majority of what
    /// this surface adds over the per-project Runs list.
    /// </summary>
    internal sealed record WorkEntry(
        string? VendorStoryId,
        string? Title,
        bool Held,
        int? ChangeNumber,
        IReadOnlyList<RunEntry> Runs
    );

    /// <summary>
    /// <paramref name="State"/> travels as the enum's name because the portal renders states by
    /// name already; the copy itself resolves in the catalogue, never from this string.
    /// </summary>
    internal sealed record RunEntry(Guid RunId, string State, DateTimeOffset CreatedAt);

    internal sealed class Handler(
        RunsDbContext database,
        IStoryReader stories,
        IProjectCatalog projects,
        IProjectPermissions permissions
    ) : IAppQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            // BR-009, scoped exactly as the inbox is (#13): every entry carries a Story title, so an
            // unscoped read would hand a caller the contents of other people's backlogs. Null means
            // ALL — the owner, the self-host habitat — and because this surface asks each project a
            // question rather than filtering one query, "all" needs a real list: the catalogue's,
            // active projects only (the shape GetInboxChanges established).
            var visible = await permissions.VisibleProjects(cancellationToken);
            var scope =
                visible?.ToList()
                ?? await projects.ActiveProjectIds(cancellationToken) as IReadOnlyList<Guid>;

            if (scope.Count == 0)
            {
                return new Response([]);
            }

            // One query for every project's live Runs rather than one per project. The states are
            // spelled out rather than "not terminal": Planning and AwaitingApproval are unreachable
            // (DEC-067) yet still exist on Runs recorded before it, and this surface is about what
            // can be live now. Those Runs are not lost — an AwaitingApproval Run is exactly what the
            // Inbox's approval lane still lists.
            var live = await database
                .Runs.Where(run =>
                    scope.Contains(run.ProjectId)
                    && (
                        run.State == RunState.Queued
                        || run.State == RunState.Executing
                        || run.State == RunState.AwaitingInput
                    )
                )
                .Select(run => new
                {
                    run.Id,
                    run.ProjectId,
                    run.VendorStoryId,
                    run.TargetChangeNumber,
                    run.TargetChangeTitle,
                    run.State,
                    run.CreatedAt,
                })
                .ToListAsync(cancellationToken);

            var runsByProject = live.GroupBy(run => run.ProjectId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var entries = new List<ProjectEntry>();

            foreach (var projectId in scope)
            {
                var held = await stories.Held(projectId, cancellationToken);
                runsByProject.TryGetValue(projectId, out var projectRuns);
                projectRuns ??= [];

                if (held.Count == 0 && projectRuns.Count == 0)
                {
                    // Absent, not empty. A quiet project contributes its row and nothing else, and
                    // the row comes from the projects list the tree already renders.
                    continue;
                }

                var work = new List<WorkEntry>();
                var heldTitles = held.ToDictionary(
                    story => story.VendorStoryId,
                    story => story.Title,
                    StringComparer.Ordinal
                );

                // Story nodes: the held ones, plus any Story with a live Run. A Story can be both,
                // and is then one node — the hold and the Run are two facts about one subject.
                var storyIds = new List<string>(heldTitles.Keys);
                foreach (var run in projectRuns)
                {
                    if (run.VendorStoryId is { } id && !heldTitles.ContainsKey(id))
                    {
                        if (!storyIds.Contains(id, StringComparer.Ordinal))
                        {
                            storyIds.Add(id);
                        }
                    }
                }

                foreach (var vendorStoryId in storyIds)
                {
                    // The title is already known for a held Story; only a running-but-unheld one
                    // needs a lookup. Per-story reads through Contracts, like the inbox's: the count
                    // is bounded by BR-002's per-project Run cap, so this is human-scale by
                    // construction rather than by hope.
                    var title = heldTitles.TryGetValue(vendorStoryId, out var known)
                        ? known
                        : (await stories.Find(projectId, vendorStoryId, cancellationToken))?.Title;

                    work.Add(
                        new WorkEntry(
                            vendorStoryId,
                            title,
                            heldTitles.ContainsKey(vendorStoryId),
                            ChangeNumber: null,
                            [
                                .. projectRuns
                                    .Where(run =>
                                        string.Equals(
                                            run.VendorStoryId,
                                            vendorStoryId,
                                            StringComparison.Ordinal
                                        )
                                    )
                                    .OrderByDescending(run => run.CreatedAt)
                                    .Select(run => new RunEntry(
                                        run.Id,
                                        run.State.ToString(),
                                        run.CreatedAt
                                    )),
                            ]
                        )
                    );
                }

                // Change nodes: a change-targeted Run (run-on-a-pr) has no Story to nest under, and
                // dropping it would make a panel about live work silent about work that is live.
                foreach (
                    var group in projectRuns
                        .Where(run =>
                            run.VendorStoryId is null && run.TargetChangeNumber is not null
                        )
                        .GroupBy(run => run.TargetChangeNumber!.Value)
                )
                {
                    work.Add(
                        new WorkEntry(
                            VendorStoryId: null,
                            group
                                .Select(run => run.TargetChangeTitle)
                                .FirstOrDefault(title => title is not null),
                            Held: false,
                            group.Key,
                            [
                                .. group
                                    .OrderByDescending(run => run.CreatedAt)
                                    .Select(run => new RunEntry(
                                        run.Id,
                                        run.State.ToString(),
                                        run.CreatedAt
                                    )),
                            ]
                        )
                    );
                }

                // The name is read only for projects that made it this far: a quiet project should
                // cost no lookup at all.
                var projectName = await projects.Name(projectId, cancellationToken);

                entries.Add(
                    new ProjectEntry(
                        projectId,
                        projectName,
                        // Newest work first, and a held Story with no Run last within its project —
                        // it has no timestamp of its own, and inventing one to sort by would be a
                        // fact the data does not carry.
                        [
                            .. work.OrderByDescending(entry =>
                                entry.Runs.Count == 0
                                    ? DateTimeOffset.MinValue
                                    : entry.Runs.Max(run => run.CreatedAt)
                            ),
                        ]
                    )
                );
            }

            return new Response([
                .. entries.OrderBy(entry => entry.ProjectName, StringComparer.OrdinalIgnoreCase),
            ]);
        }
    }
}
