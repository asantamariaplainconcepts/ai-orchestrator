using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Connectors;
using AiOrchestrator.Modules.Backlog.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog.UseCases;

/// <summary>
/// What a credential needs to be granted for a configuration (#226). Answered for a *proposed*
/// shape rather than for the stored Connector, because the question is asked while filling the
/// form — before anything is saved, and often while changing the very fields that decide it.
/// <para>
/// It reads the same <see cref="ConnectorCapabilities"/> set verification probes, so what the
/// product asks for and what it checks cannot drift: a capability with no scope name does not
/// compile, and a capability nobody probes cannot appear here (design D1/D4).
/// </para>
/// </summary>
sealed class GetRequiredPermissions : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/connector/required-permissions",
                async (
                    Guid projectId,
                    string? codeSource,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                    Results.Ok(
                        await sender.Send(new Query(projectId, codeSource), cancellationToken)
                    )
            )
            .WithName(nameof(GetRequiredPermissions))
            .WithTags("Backlog");

    /// <summary>
    /// <paramref name="Scopes"/> is in the vendor's own vocabulary — what a person selects while
    /// minting a token — because a list in the product's internal capability names would have to
    /// be translated by the reader before it could be acted on.
    /// </summary>
    internal sealed record Response(IReadOnlyList<string> Scopes);

    [Requires(BacklogPermissions.Configure)]
    internal sealed record Query(Guid ProjectId, string? CodeSource)
        : IQuery<Response>,
            IScopedToProject;

    internal sealed class Handler : IAppQueryHandler<Query, Response>
    {
        public Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            // Absent or unparseable means the default, exactly as the configure validator treats
            // it: this read must never disagree with the save about which shape it is describing.
            var codeSource = Enum.TryParse<CodeSource>(
                query.CodeSource,
                ignoreCase: true,
                out var parsed
            )
                ? parsed
                : CodeSource.Repository;

            var scopes = ConnectorCapabilities
                .For(codeSource)
                .Select(capability => capability.GitHubScope)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return Task.FromResult(new Response(scopes));
        }
    }
}
