using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog.UseCases;

/// <summary>
/// Whether a secret name resolves on this deployment (design review 5d). BR-010's split — the
/// name lives here, the value lives in the environment — is where the self-host quickstart loses
/// people today: a name that resolves to nothing is only discovered when the Connector fails.
/// This answers while the name is being typed, through the same seam every real resolution uses,
/// so the check and the eventual read cannot disagree.
/// <para>
/// Existence only, ever: the response is one boolean, the value never leaves the resolver, and
/// nothing here logs it. Answered for a *proposed* name rather than the stored Connector, for
/// the same reason <see cref="GetRequiredPermissions"/> is — the question is asked mid-form.
/// </para>
/// </summary>
sealed class CheckSecretResolution : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/connector/secret-resolves",
                async (
                    Guid projectId,
                    string? name,
                    ISender sender,
                    CancellationToken cancellationToken
                ) => Results.Ok(await sender.Send(new Query(projectId, name), cancellationToken))
            )
            .WithName(nameof(CheckSecretResolution))
            .WithTags("Backlog");

    internal sealed record Response(bool Resolves);

    [Requires(BacklogPermissions.Configure)]
    internal sealed record Query(Guid ProjectId, string? Name) : IQuery<Response>, IScopedToProject;

    internal sealed class Handler(ISecretResolver secrets) : IAppQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query.Name))
            {
                return new Response(Resolves: false);
            }

            try
            {
                // The value is read and discarded on purpose: "resolves" has to mean the same
                // thing here as at the poller's first real use, and only the seam knows that.
                _ = await secrets.Resolve(query.Name.Trim(), cancellationToken);
                return new Response(Resolves: true);
            }
            catch (SecretNotFoundException)
            {
                // The one expected miss. Anything else — an unreachable vault, a broken key
                // ring — is an error, not "doesn't resolve yet", and must surface as one.
                return new Response(Resolves: false);
            }
        }
    }
}
