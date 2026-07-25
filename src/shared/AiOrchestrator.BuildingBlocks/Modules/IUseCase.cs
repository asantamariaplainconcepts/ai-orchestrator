namespace AiOrchestrator.BuildingBlocks.Modules;

/// <summary>
/// Marker for a vertical slice. Implementations are <c>sealed</c> and expose a
/// <c>public static void AddRoutes(IEndpointRouteBuilder)</c>, discovered by <see cref="ModuleBase"/>.
/// </summary>
public interface IUseCase;
