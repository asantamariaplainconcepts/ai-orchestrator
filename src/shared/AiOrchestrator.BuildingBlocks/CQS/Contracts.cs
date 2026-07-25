namespace AiOrchestrator.BuildingBlocks.CQS;

/// <summary>A state-changing request. Implementations are <c>internal</c> (enforced by MOD001).</summary>
public interface ICommand<TResponse>;

/// <summary>A read-only request. Implementations are <c>internal</c> (enforced by MOD001).</summary>
public interface IQuery<TResponse>;

/// <summary>Handles a command. Implementations are <c>internal sealed</c> (enforced by CQS001).</summary>
public interface IAppCommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<TResponse> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>Handles a query. Implementations are <c>internal sealed</c> (enforced by CQS001).</summary>
public interface IAppQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<TResponse> Handle(TQuery query, CancellationToken cancellationToken);
}

/// <summary>Dispatches requests through the decorator pipeline. The only entry point for use cases.</summary>
public interface ISender
{
    Task<TResponse> Send<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default
    );

    Task<TResponse> Send<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Opt-in marker: a query carrying this is served from cache by the caching decorator.
/// </summary>
public interface ICachedQuery
{
    string CacheKey { get; }

    TimeSpan? Expiration => null;
}

/// <summary>
/// Opt-in marker: a command carrying this evicts the listed keys after the handler succeeds.
/// </summary>
public interface ICacheInvalidator
{
    IReadOnlyCollection<string> CacheKeysToInvalidate { get; }
}
