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
        IReadOnlyList<string> Unmatched
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

                    return new Candidate(
                        listing.Directory,
                        listing.Files,
                        [
                            .. matched
                                .Where(pair => pair.Step is not null)
                                .Select(pair => pair.Step!.Trigger)
                                .Distinct(StringComparer.OrdinalIgnoreCase),
                        ],
                        [.. matched.Where(pair => pair.Step is null).Select(pair => pair.File)]
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
