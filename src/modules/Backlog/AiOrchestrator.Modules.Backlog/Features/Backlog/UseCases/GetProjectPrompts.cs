using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog.UseCases;

/// <summary>
/// #215 — the prompts that actually exist, for the Automation form's picker. Read live from the
/// repository's default branch through the Connector; never cached, never mirrored.
/// <para>
/// Degradation is data, not a 500 (design D3): "no Connector", "the vendor refused" and "nothing
/// there yet" are ordinary outcomes the form renders as today's textbox plus a reason. A picker
/// that throws teaches the form to treat discovery as load-bearing, and discovery is a
/// convenience — the save path never learns about this listing (design D4).
/// </para>
/// </summary>
sealed class GetProjectPrompts : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/prompts",
                async (Guid projectId, ISender sender, CancellationToken cancellationToken) =>
                    Results.Ok(await sender.Send(new Query(projectId), cancellationToken))
            )
            .WithName(nameof(GetProjectPrompts))
            .WithTags("Backlog");

    /// <summary>
    /// <see cref="Reason"/> is null when the listing worked — an empty <see cref="Names"/> then
    /// honestly means "nothing there yet". A non-null reason says why there is no listing at all.
    /// </summary>
    internal sealed record Response(string Directory, IReadOnlyList<string> Names, string? Reason);

    [Requires(BacklogPermissions.Read)]
    internal sealed record Query(Guid ProjectId) : IQuery<Response>, IScopedToProject;

    internal sealed class Handler(ConnectorAccess access) : IAppQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            var context = await access.Resolve(query.ProjectId, cancellationToken);
            if (context.IsError)
            {
                // No Connector (or an unresolvable credential) degrades, never refuses: looking
                // at the form before configuring a Connector is an ordinary first step.
                return new Response(
                    PromptPath.NormalizeDirectory(null),
                    [],
                    context.FirstError.Description
                );
            }

            var (connector, coordinates, token) = context.Value;
            var directory = PromptPath.NormalizeDirectory(context.Value.PromptDirectory);

            var listing = await connector.ListDirectoryFiles(
                coordinates,
                directory,
                token,
                cancellationToken
            );

            if (listing.IsError)
            {
                return new Response(directory, [], listing.FirstError.Description);
            }

            // An absent directory and an empty one both read "nothing there yet" — the honest
            // empty state, not a failure (#215 spec).
            return new Response(
                directory,
                [
                    .. (listing.Value?.Files ?? []).Where(name =>
                        name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                    ),
                ],
                Reason: null
            );
        }
    }
}
