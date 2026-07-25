using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.BuildingBlocks.CQS;

sealed class Sender(IServiceProvider services) : ISender
{
    static readonly ConcurrentDictionary<Type, HandlerInvoker> Invokers = new();

    public Task<TResponse> Send<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default
    ) => Dispatch<TResponse>(command, typeof(IAppCommandHandler<,>), cancellationToken);

    public Task<TResponse> Send<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken = default
    ) => Dispatch<TResponse>(query, typeof(IAppQueryHandler<,>), cancellationToken);

    Task<TResponse> Dispatch<TResponse>(
        object request,
        Type openHandlerType,
        CancellationToken cancellationToken
    )
    {
        var invoker = Invokers.GetOrAdd(
            request.GetType(),
            static (requestType, state) =>
            {
                var handlerType = state.OpenHandlerType.MakeGenericType(
                    requestType,
                    state.ResponseType
                );
                var method =
                    handlerType.GetMethod(
                        nameof(IAppCommandHandler<ICommand<object>, object>.Handle)
                    ) ?? throw new InvalidOperationException($"No Handle method on {handlerType}.");
                return new HandlerInvoker(handlerType, method);
            },
            (OpenHandlerType: openHandlerType, ResponseType: typeof(TResponse))
        );

        var handler =
            services.GetService(invoker.HandlerType)
            ?? throw new InvalidOperationException(
                $"No handler registered for {request.GetType().Name}. "
                    + "Handlers are discovered by assembly scan — check the type is internal sealed and implements the handler interface."
            );

        return (Task<TResponse>)invoker.Method.Invoke(handler, [request, cancellationToken])!;
    }

    sealed record HandlerInvoker(Type HandlerType, MethodInfo Method);
}
