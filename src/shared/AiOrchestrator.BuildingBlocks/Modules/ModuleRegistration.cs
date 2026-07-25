using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.BuildingBlocks.Modules;

/// <summary>
/// Runtime module discovery: the host composes whatever <c>AiOrchestrator.Modules.*.dll</c> ships
/// beside it. Contracts assemblies are excluded — they carry no implementation.
/// </summary>
public static class ModuleRegistration
{
    public const string ModuleAssemblyPrefix = "AiOrchestrator.Modules.";
    const string ContractsAssemblySuffix = ".Contracts";

    public static IReadOnlyList<IModule> Discover()
    {
        var directory = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;

        return
        [
            .. Directory
                .EnumerateFiles(directory, $"{ModuleAssemblyPrefix}*.dll")
                .Where(path =>
                    !Path.GetFileNameWithoutExtension(path)
                        .EndsWith(ContractsAssemblySuffix, StringComparison.Ordinal)
                )
                .Select(Assembly.LoadFrom)
                .SelectMany(assembly => assembly.GetExportedTypes())
                .Where(type =>
                    typeof(IModule).IsAssignableFrom(type)
                    && type is { IsAbstract: false, IsInterface: false }
                )
                .Select(type => (IModule)Activator.CreateInstance(type)!)
                .OrderBy(module => module.Name, StringComparer.Ordinal),
        ];
    }

    public static IServiceCollection AddModules(
        this IServiceCollection services,
        IReadOnlyList<IModule> modules,
        IConfiguration configuration
    )
    {
        foreach (var module in modules)
        {
            module.Add(services, configuration);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapModules(
        this IEndpointRouteBuilder endpoints,
        IReadOnlyList<IModule> modules
    )
    {
        foreach (var module in modules)
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }

    public static Assembly[] Assemblies(this IReadOnlyList<IModule> modules) =>
        [.. modules.Select(module => module.GetType().Assembly).Distinct()];
}
