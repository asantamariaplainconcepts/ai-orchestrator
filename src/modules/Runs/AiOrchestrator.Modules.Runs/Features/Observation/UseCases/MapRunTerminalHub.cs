using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Runs.Features.Observation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;

namespace AiOrchestrator.Modules.Runs.Features.Observation.UseCases;

/// <summary>
/// Where a Run's terminal is reachable (#304). A use case only in the mapping sense, exactly as
/// <see cref="MapRunLogHub"/> is: the hub carries no request and no response of its own.
/// </summary>
sealed class MapRunTerminalHub : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints.MapHub<RunTerminalHub>("/hubs/run-terminal");
}
