using AiOrchestrator.BuildingBlocks.CQS;
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
    internal sealed record Response(
        IReadOnlyList<Candidate> Candidates,
        IReadOnlyList<string> SearchedIn,
        string? Reason
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
    /// <paramref name="OutputLabels"/> is what this step hands on (#262). The card needs it to say,
    /// as rows are deselected, that a hand-off no longer happens — and that answer has to arrive on
    /// a click, so it cannot be a round trip. The labels come from the catalogue the plan is already
    /// walking, so carrying them costs nothing.
    /// </para>
    /// </summary>
    internal sealed record PlannedStep(
        string Trigger,
        string PromptFile,
        bool Exists,
        bool Gated,
        bool Installable,
        IReadOnlyList<string> OutputLabels
    );

    [Requires(ProjectPermissions.ManageAutomations)]
    internal sealed record Query(Guid ProjectId) : IQuery<Response>, IScopedToProject;

    internal sealed class Handler(IConnectorReader connectors, PipelineDiscovery discovery)
        : IAppQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            var connector = await connectors.Find(query.ProjectId, cancellationToken);
            if (connector is null)
            {
                // Not an error: a project reaches this screen before it is connected, and the
                // honest answer is that there is nowhere to look yet.
                return new Response([], [], "this project has no Connector yet");
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

                    var plan = PipelineSteps
                        .All.Select(step =>
                        {
                            var exists = present.TryGetValue(step.Trigger, out var file);
                            var installable = PipelineSteps.Installable.Any(candidate =>
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
                                step.Wiring.RequiresApproval,
                                installable,
                                step.Wiring.OutputLabels
                            );
                        })
                        .Where(step => step.Exists || step.Installable)
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
                refusal
            );
        }
    }
}
