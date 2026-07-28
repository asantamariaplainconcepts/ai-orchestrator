using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Runs.Features.Observation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;

namespace AiOrchestrator.Modules.Runs.Features.Observation.UseCases;

/// <summary>
/// Where the live window is reachable (#106). A use case only in the mapping sense — it owns no
/// query and no command, because the hub carries no request/response of its own.
/// </summary>
sealed class MapRunLogHub : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints.MapHub<RunLogHub>("/hubs/run-log");
}
