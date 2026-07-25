using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.BuildingBlocks.CQS;

public static class CqsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the CQS pipeline with its fixed decorator order:
    /// Logging -> Validation -> Caching -> Handler -> InvalidateCaching.
    /// The order is owned here and is not configurable per module or call site.
    /// </summary>
    public static IServiceCollection AddVsaCqsArchitecture(
        this IServiceCollection services,
        params Assembly[] assemblies
    )
    {
        services.AddMemoryCache();
        services.AddSingleton<ISender, Sender>();

        services.Scan(scan =>
            scan.FromAssemblies(assemblies)
                .AddClasses(
                    classes => classes.AssignableTo(typeof(IAppCommandHandler<,>)),
                    publicOnly: false
                )
                .AsImplementedInterfaces()
                .WithScopedLifetime()
                .AddClasses(
                    classes => classes.AssignableTo(typeof(IAppQueryHandler<,>)),
                    publicOnly: false
                )
                .AsImplementedInterfaces()
                .WithScopedLifetime()
        );

        services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);

        // Innermost first: each Decorate call wraps everything registered before it.
        services.Decorate(
            typeof(IAppCommandHandler<,>),
            typeof(CacheInvalidationCommandHandlerDecorator<,>)
        );
        services.Decorate(
            typeof(IAppCommandHandler<,>),
            typeof(ValidationCommandHandlerDecorator<,>)
        );
        services.Decorate(typeof(IAppCommandHandler<,>), typeof(LoggingCommandHandlerDecorator<,>));

        services.Decorate(typeof(IAppQueryHandler<,>), typeof(CachingQueryHandlerDecorator<,>));
        services.Decorate(typeof(IAppQueryHandler<,>), typeof(ValidationQueryHandlerDecorator<,>));
        services.Decorate(typeof(IAppQueryHandler<,>), typeof(LoggingQueryHandlerDecorator<,>));

        return services;
    }
}
