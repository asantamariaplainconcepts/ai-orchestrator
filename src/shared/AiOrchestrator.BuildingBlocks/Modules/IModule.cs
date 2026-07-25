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

    /// <summary>
    /// Brings the module's own schema up to date. Each module owns its migrations, so the host
    /// cannot do this for them — it only decides when it is allowed to happen.
    /// </summary>
    Task Migrate(IServiceProvider services, CancellationToken cancellationToken);
}
