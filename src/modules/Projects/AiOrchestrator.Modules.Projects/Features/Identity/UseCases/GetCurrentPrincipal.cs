using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Projects.Features.Identity.UseCases;

/// <summary>
/// Who the portal is talking to (#119). No query handler and no database: the answer is the
/// habitat's composition, and reading it through the seam is the whole point — the page shows
/// what the server believes rather than a string the page decided for itself.
/// </summary>
sealed class GetCurrentPrincipal : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/me",
                (ICurrentPrincipal principal) =>
                {
                    var current = principal.Current;
                    return Results.Ok(
                        new Response(current.Id, current.DisplayName, current.Role.ToString())
                    );
                }
            )
            .WithName(nameof(GetCurrentPrincipal))
            .WithTags("Identity");

    internal sealed record Response(string Id, string DisplayName, string Role);
}
