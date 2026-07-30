using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Features.Observation.UseCases;

/// <summary>
/// UC-026 — everything waiting on a human, in one place. Three states qualify: a plan awaiting
/// approval (DEC-040), a question awaiting an answer (#78), and a failure awaiting a decision
/// (BR-004). Cross-project on purpose: humans are the bottleneck this list feeds, and they do
/// not think in project boundaries when asking "what needs me?".
/// <para>
/// A Failed Run leaves the list when a newer Run exists for its Story — derived by query, never
/// stored as a flag, because BR-013's two legitimate re-trigger paths (Run now, re-labelling)
/// would both forget to update one (design D2). It also leaves when a human dismissed it, and
/// <i>that</i> one is stored, because no query can tell "nobody decided yet" from "somebody decided
/// not to act" (#145). The predicate itself lives in <see cref="WaitingRuns"/>, shared with the
/// pulse's count so the two cannot drift.
/// </para>
/// </summary>
sealed class GetInbox : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/inbox",
                async (ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(GetInbox))
            .WithTags("Runs");

    [Requires(Access.FiltersToCaller)]
    internal sealed record Query : IQuery<IReadOnlyList<Entry>>;

    /// <summary>
    /// <paramref name="WaitingFor"/> is one of <c>approval</c>, <c>input</c>, <c>failure</c> —
    /// the reason vocabulary, not the state enum, so the UI never switches on internal names.
    /// </summary>
    internal sealed record Entry(
        Guid RunId,
        Guid ProjectId,
        string VendorStoryId,
        string? StoryTitle,
        string WaitingFor,
        DateTimeOffset WaitingSince
    );

    internal sealed class Handler(
        RunsDbContext database,
        IStoryReader stories,
        IProjectPermissions permissions
    ) : IAppQueryHandler<Query, IReadOnlyList<Entry>>
    {
        public async Task<IReadOnlyList<Entry>> Handle(
            Query query,
            CancellationToken cancellationToken
        )
        {
            // Cross-project, and therefore scoped here rather than by a project in the request
            // (#13): every entry carries a Story id and title, so an unfiltered inbox would hand a
            // caller with no roles the contents of other people's backlogs.
            var visible = await permissions.VisibleProjects(cancellationToken);

            var waiting = database.Runs.WaitingOnAHuman();
            if (visible is not null)
            {
                waiting = waiting.Where(run => visible.Contains(run.ProjectId));
            }

            var runs = await waiting.ToListAsync(cancellationToken);

            var entries = new List<Entry>(runs.Count);

            foreach (var run in runs)
            {
                // Per-entry lookup through Contracts (design D4): the list is human-scale by
                // nature, and a denormalised title on the Run would mirror the mirror (BR-008).
                var story = await stories.Find(run.ProjectId, run.VendorStoryId, cancellationToken);

                entries.Add(
                    new Entry(
                        run.Id,
                        run.ProjectId,
                        run.VendorStoryId,
                        story?.Title,
                        run.State switch
                        {
                            RunState.AwaitingApproval => "approval",
                            RunState.AwaitingInput => "input",
                            _ => "failure",
                        },
                        // The age comes from the state that defines the wait (design D3) — each
                        // timestamp already exists because its state already needed it.
                        run.State switch
                        {
                            RunState.AwaitingInput => run.WaitingSince ?? run.CreatedAt,
                            RunState.Failed => run.EndedAt ?? run.CreatedAt,
                            _ => run.StartedAt ?? run.CreatedAt,
                        }
                    )
                );
            }

            return [.. entries.OrderByDescending(entry => entry.WaitingSince)];
        }
    }
}
