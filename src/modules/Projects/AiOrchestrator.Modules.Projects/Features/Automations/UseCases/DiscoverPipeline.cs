using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Domain;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Projects.Features.Automations.UseCases;

/// <summary>
/// #229 — what pipeline does this repository already have? Asked before anything is created, and
/// answered without writing anything, because discovery proposes and never picks (design D1).
/// <para>
/// A read on purpose: the Admin sees every candidate that holds prompt files, with the steps each
/// one covers, and confirms one. Choosing the richest candidate automatically would silently
/// reconfigure a project the first time somebody pressed a button, and the only thing worse than
/// not finding a pipeline is adopting the wrong one.
/// </para>
/// </summary>
sealed class DiscoverPipeline : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/automations/discover-pipeline",
                async (Guid projectId, ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new Query(projectId), cancellationToken))
            )
            .WithName(nameof(DiscoverPipeline))
            .WithTags("Automations");

    /// <summary>
    /// <paramref name="SearchedIn"/> is present even when nothing was found: "we looked in these
    /// three places" is an answer, and a bare empty list reads as a broken button.
    /// </summary>
    /// <param name="Tiers">
    /// The catalogue's tiers (#269), so the card can offer a consent that states its own consequence.
    /// Carried here for the reason #262 carried output labels: the answer to "what would this write?"
    /// has to arrive on a click, so it cannot be a round trip — and the data sits on the catalogue the
    /// plan projection already walks, so it costs nothing.
    /// </param>
    internal sealed record Response(
        IReadOnlyList<Candidate> Candidates,
        IReadOnlyList<string> SearchedIn,
        string? Reason,
        IReadOnlyList<Tier> Tiers
    );

    /// <summary>
    /// One starter tier as a consent decision. <paramref name="Requires"/> is null for a tier that
    /// needs nothing beyond the repository — such a tier needs no consent, and the card shows no
    /// control for it.
    /// </summary>
    internal sealed record Tier(
        string Id,
        string Title,
        string Summary,
        string? Requires,
        IReadOnlyList<string> Prerequisites
    );

    /// <summary>
    /// One directory that holds prompt files. <paramref name="Unmatched"/> is reported rather than
    /// interpreted — a file matching no step is somebody's document, not a trigger to invent.
    /// </summary>
    internal sealed record Candidate(
        string Directory,
        IReadOnlyList<string> Files,
        IReadOnlyList<string> Steps,
        IReadOnlyList<string> Unmatched,
        IReadOnlyList<PlannedStep> Plan
    );

    /// <summary>
    /// One row of what pressing the button would create (#233), computed from the listing already
    /// read — no second endpoint and no extra vendor call.
    /// <para>
    /// <paramref name="Exists"/> distinguishes "this repository already has the file" from "a
    /// starter would be installed", which is the difference between a wiring and a repository
    /// write. <paramref name="Installable"/> is false for a step whose tier requires something this
    /// project may not have: it can be wired to a file that exists, but no starter will be written
    /// for it.
    /// </para>
    /// <para>
    /// <paramref name="ToStage"/> is the transition this step claims (#262, restated for #310). The
    /// card needs it to say, as rows are deselected, that a hand-off no longer happens — and that
    /// answer has to arrive on a click, so it cannot be a round trip. It comes from the catalogue the
    /// plan is already walking, so carrying it costs nothing. Null means the step claims no transition,
    /// which is not a gap in the plan: it is a step the flow ends at.
    /// </para>
    /// </summary>
    /// <param name="Installable">
    /// Whether a starter could be written for this step at all — true for a step whose tier needs no
    /// consent. Since #269 a gated step is installable <i>once its tier is consented to</i>, and that
    /// decision belongs to the card, which is why <paramref name="TierId"/> travels beside this.
    /// </param>
    /// <param name="TierId">
    /// The tier this step came from, so toggling a consent adds and removes its rows without a round
    /// trip.
    /// </param>
    /// <param name="Holds">
    /// Whether this step stops for a person: its marks include the hold, so the Story it finishes
    /// with starts nothing until somebody clears it (BR-007, DEC-067). It replaced an approval flag
    /// — the wait moved from inside the step's Run to the boundary after it.
    /// </param>
    internal sealed record PlannedStep(
        string Trigger,
        string PromptFile,
        bool Exists,
        bool Holds,
        bool Installable,
        string? ToStage,
        string TierId
    );

    [Requires(ProjectPermissions.ManageAutomations)]
    internal sealed record Query(Guid ProjectId) : IQuery<Response>, IScopedToProject;

    internal sealed class Handler(IConnectorReader connectors, PipelineDiscovery discovery)
        : IAppQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            // The tiers are catalogue content and do not depend on the repository, so they are
            // answered even where there is nothing to look in: an Admin may read what a consent
            // would write before connecting anything.
            var tiers = Tiers();

            var connector = await connectors.Find(query.ProjectId, cancellationToken);
            if (connector is null)
            {
                // Not an error: a project reaches this screen before it is connected, and the
                // honest answer is that there is nowhere to look yet.
                return new Response([], [], "this project has no Connector yet", tiers);
            }

            var listings = await discovery.Candidates(
                query.ProjectId,
                connector.PromptDirectory,
                cancellationToken
            );

            var refusal = listings.FirstOrDefault(listing => listing.Failure is not null)?.Failure;

            var candidates = listings
                .Where(listing => listing.Files.Count > 0)
                .Select(listing =>
                {
                    var matched = listing
                        .Files.Select(file => (File: file, Step: PipelineSteps.Match(file)))
                        .ToList();

                    // The plan the button would carry out, said before it is pressed rather than
                    // reported after (#233). Two kinds of step appear: the ones this directory
                    // already has a file for, and the ones a starter would be installed for. A step
                    // that is neither is left out — nothing would happen for it either way, and
                    // since #262 made every row a choice, a row whose choice changes nothing is
                    // noise in a list whose whole job is to say what the press will do.
                    var present = matched
                        .Where(pair => pair.Step is not null)
                        .ToDictionary(
                            pair => pair.Step!.Trigger,
                            pair => pair.File,
                            StringComparer.OrdinalIgnoreCase
                        );

                    // Every step is planned, and the card decides which rows to show (#269). Before
                    // consent existed this filtered out steps that were neither present nor
                    // installable, because nothing would happen for them either way — but a gated
                    // step is now installable the moment its tier is consented to, so dropping it
                    // here would hide the row a consent is supposed to reveal. `Installable` keeps
                    // its meaning — installable *without* consent — and `TierId` is what lets the
                    // card add the rest without asking again.
                    var uncontested = PipelineSteps.Installable(null);

                    var plan = PipelineSteps
                        .All.Select(step =>
                        {
                            var exists = present.TryGetValue(step.Trigger, out var file);
                            var installable = uncontested.Any(candidate =>
                                string.Equals(
                                    candidate.Trigger,
                                    step.Trigger,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            );

                            return new PlannedStep(
                                step.Trigger,
                                exists ? file! : step.Prompt.SaveAs,
                                exists,
                                StoryHold.IsHeld(step.Wiring.Marks),
                                installable,
                                step.Wiring.ToStage,
                                step.Tier.Id
                            );
                        })
                        .ToList();

                    return new Candidate(
                        listing.Directory,
                        listing.Files,
                        [
                            .. matched
                                .Where(pair => pair.Step is not null)
                                .Select(pair => pair.Step!.Trigger)
                                .Distinct(StringComparer.OrdinalIgnoreCase),
                        ],
                        [.. matched.Where(pair => pair.Step is null).Select(pair => pair.File)],
                        plan
                    );
                })
                .ToList();

            return new Response(
                candidates,
                PipelineDiscovery.Roots(connector.PromptDirectory),
                refusal,
                tiers
            );
        }

        /// <summary>
        /// The catalogue's tiers with the paths each one's consent would write. Content, not a read:
        /// no vendor call, which is the constraint the plan requirement already imposes on this
        /// endpoint.
        /// </summary>
        static IReadOnlyList<Tier> Tiers() =>
            [
                .. StarterCatalogue.Tiers.Select(tier => new Tier(
                    tier.Id,
                    tier.Title,
                    tier.Summary,
                    tier.Requires,
                    [.. tier.Prerequisites.Select(prerequisite => prerequisite.Path)]
                )),
            ];
    }
}
