using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Projects.Features.Automations.UseCases;

/// <summary>
/// #190 — the starter set, offered against a project.
/// <para>
/// Project-scoped even though the content is not, because the useful answer is not the list: it is
/// the list <i>against this project</i> — which of these do you already have, and where would they
/// go (design D5). A global catalogue would need a second call to answer the only question that
/// matters, and a second permission for the same act.
/// </para>
/// <para>
/// <b>Nothing here writes.</b> No agent pass, no repository write, no state. The reads are the same
/// prompt reads a Run performs, used to report collisions rather than to run anything.
/// </para>
/// </summary>
sealed class ListStarterPrompts : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/starter-prompts",
                async (Guid projectId, ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new Query(projectId), cancellationToken))
            )
            .WithName(nameof(ListStarterPrompts))
            .WithTags("Automations");

    /// <summary>
    /// The permission a Member already holds to read this project's Automations. Taking a starter is
    /// writing a file in a repository the caller has their own access to; the product is showing
    /// content, and gating that behind the manage permission would say the catalogue is more
    /// sensitive than the Automations it exists to feed.
    /// </summary>
    [Requires(ProjectPermissions.ReadAutomations)]
    internal sealed record Query(Guid ProjectId)
        : IQuery<IReadOnlyList<StarterTierResponse>>,
            IScopedToProject;

    internal sealed class Handler(IDocumentReader documents)
        : IAppQueryHandler<Query, IReadOnlyList<StarterTierResponse>>
    {
        public async Task<IReadOnlyList<StarterTierResponse>> Handle(
            Query query,
            CancellationToken cancellationToken
        )
        {
            var tiers = new List<StarterTierResponse>();

            foreach (var tier in StarterCatalogue.Tiers)
            {
                var prompts = new List<StarterPromptResponse>();

                foreach (var prompt in tier.Prompts)
                {
                    var (path, present) = await Presence(
                        documents,
                        query.ProjectId,
                        prompt.SaveAs,
                        cancellationToken
                    );

                    prompts.Add(
                        new StarterPromptResponse(
                            prompt.File,
                            prompt.SaveAs,
                            prompt.Purpose,
                            prompt.Assumes,
                            prompt.Content,
                            path,
                            present
                        )
                    );
                }

                tiers.Add(
                    new StarterTierResponse(
                        tier.Id,
                        tier.Title,
                        tier.Summary,
                        tier.Requires,
                        prompts
                    )
                );
            }

            return tiers;
        }

        /// <summary>
        /// One read per starter, through the same prompt read a Run uses — so the path reported is
        /// the path a Run would resolve, by construction rather than by a second implementation
        /// agreeing with the first.
        /// <para>
        /// The tri-state comes out of the existing contract without changing it. A resolved path
        /// means the Connector answered and the question is real: content present, or absent. No
        /// resolved path means the read never reached the vendor — no Connector, or a directory that
        /// refused — and presence is <b>unknown</b>. Rendering that as "you do not have this" would
        /// be a claim nobody checked, which is precisely what BR-011 forbids about cost.
        /// </para>
        /// </summary>
        static async Task<(string? Path, bool? Present)> Presence(
            IDocumentReader documents,
            Guid projectId,
            string saveAs,
            CancellationToken cancellationToken
        )
        {
            var result = await documents.ReadPrompt(projectId, saveAs, cancellationToken);

            return result.ResolvedPath is null
                ? (null, null)
                : (result.ResolvedPath, result.Content is not null);
        }
    }
}
