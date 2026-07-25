using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.BuildingBlocks.Modules;

/// <summary>
/// A self-registering module. The host discovers implementations in
/// <c>AiOrchestrator.Modules.*.dll</c> at startup; adding a module requires no host edits.
/// </summary>
public interface IModule
{
    string Name { get; }

    void Add(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
