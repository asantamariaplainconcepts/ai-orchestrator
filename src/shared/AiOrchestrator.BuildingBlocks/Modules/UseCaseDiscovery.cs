using System.Reflection;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.BuildingBlocks.Modules;

public static class UseCaseDiscovery
{
    public const string AddRoutesMethodName = "AddRoutes";

    /// <summary>
    /// Every <see cref="IUseCase"/> in <paramref name="assembly"/> exposing a static
    /// <c>AddRoutes(IEndpointRouteBuilder)</c>.
    /// </summary>
    public static IEnumerable<MethodInfo> FindIn(Assembly assembly) =>
        assembly
            .GetTypes()
            .Where(type =>
                typeof(IUseCase).IsAssignableFrom(type)
                && type is { IsAbstract: false, IsInterface: false }
            )
            .Select(type =>
                type.GetMethod(
                    AddRoutesMethodName,
                    BindingFlags.Public | BindingFlags.Static,
                    [typeof(IEndpointRouteBuilder)]
                )
            )
            .Where(method => method is not null)
            .Select(method => method!);
}
