using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.BuildingBlocks.Modules;

/// <summary>
/// Base for modules: maps every <see cref="IUseCase"/> found in the module's assembly, so a new
/// slice is routed by existing in it, never by editing a registration list.
/// </summary>
public abstract class ModuleBase : IModule
{
    public abstract string Name { get; }

    public abstract void Add(IServiceCollection services, IConfiguration configuration);

    public virtual void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        foreach (var useCase in UseCaseDiscovery.FindIn(GetType().Assembly))
        {
            useCase.Invoke(null, [endpoints]);
        }
    }

    /// <summary>No-op by default: a module without persistence has nothing to migrate.</summary>
    public virtual Task Migrate(IServiceProvider services, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
