using FluentValidation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.BuildingBlocks.CQS;

// The pipeline order is fixed by AddVsaCqsArchitecture and is not configurable per call site:
// Logging -> Validation -> Caching -> Handler -> InvalidateCaching.

sealed partial class LoggingCommandHandlerDecorator<TCommand, TResponse>(
    IAppCommandHandler<TCommand, TResponse> inner,
    ILogger<LoggingCommandHandlerDecorator<TCommand, TResponse>> logger
) : IAppCommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> Handle(TCommand command, CancellationToken cancellationToken)
    {
        LogHandling(logger, typeof(TCommand).Name);
        var response = await inner.Handle(command, cancellationToken);
        LogHandled(logger, typeof(TCommand).Name);
        return response;
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "Handling command {Command}")]
    static partial void LogHandling(ILogger logger, string command);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Debug, Message = "Handled command {Command}")]
    static partial void LogHandled(ILogger logger, string command);
}

sealed partial class LoggingQueryHandlerDecorator<TQuery, TResponse>(
    IAppQueryHandler<TQuery, TResponse> inner,
    ILogger<LoggingQueryHandlerDecorator<TQuery, TResponse>> logger
) : IAppQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public async Task<TResponse> Handle(TQuery query, CancellationToken cancellationToken)
    {
        LogHandling(logger, typeof(TQuery).Name);
        var response = await inner.Handle(query, cancellationToken);
        LogHandled(logger, typeof(TQuery).Name);
        return response;
    }

    [LoggerMessage(EventId = 1003, Level = LogLevel.Debug, Message = "Handling query {Query}")]
    static partial void LogHandling(ILogger logger, string query);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Debug, Message = "Handled query {Query}")]
    static partial void LogHandled(ILogger logger, string query);
}

/// <summary>
/// Input-shape validation. A failure short-circuits the pipeline as a <see cref="ValidationException"/>,
/// which the global exception handler renders as RFC 7807 ValidationProblemDetails. Domain errors are a
/// different concern and travel as <c>ErrorOr</c> results returned by the handler.
/// </summary>
sealed class ValidationCommandHandlerDecorator<TCommand, TResponse>(
    IAppCommandHandler<TCommand, TResponse> inner,
    IEnumerable<IValidator<TCommand>> validators
) : IAppCommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> Handle(TCommand command, CancellationToken cancellationToken)
    {
        await ValidationPipeline.EnsureValid(command, validators, cancellationToken);
        return await inner.Handle(command, cancellationToken);
    }
}

sealed class ValidationQueryHandlerDecorator<TQuery, TResponse>(
    IAppQueryHandler<TQuery, TResponse> inner,
    IEnumerable<IValidator<TQuery>> validators
) : IAppQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public async Task<TResponse> Handle(TQuery query, CancellationToken cancellationToken)
    {
        await ValidationPipeline.EnsureValid(query, validators, cancellationToken);
        return await inner.Handle(query, cancellationToken);
    }
}

static class ValidationPipeline
{
    public static async Task EnsureValid<TRequest>(
        TRequest request,
        IEnumerable<IValidator<TRequest>> validators,
        CancellationToken cancellationToken
    )
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken);
            if (!result.IsValid)
            {
                failures.AddRange(result.Errors);
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }
}

/// <summary>Serves queries marked <see cref="ICachedQuery"/> from memory.</summary>
sealed class CachingQueryHandlerDecorator<TQuery, TResponse>(
    IAppQueryHandler<TQuery, TResponse> inner,
    IMemoryCache cache
) : IAppQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public async Task<TResponse> Handle(TQuery query, CancellationToken cancellationToken)
    {
        if (query is not ICachedQuery cached)
        {
            return await inner.Handle(query, cancellationToken);
        }

        if (cache.TryGetValue(cached.CacheKey, out TResponse? hit) && hit is not null)
        {
            return hit;
        }

        var response = await inner.Handle(query, cancellationToken);

        using var entry = cache.CreateEntry(cached.CacheKey);
        entry.Value = response;
        if (cached.Expiration is { } expiration)
        {
            entry.AbsoluteExpirationRelativeToNow = expiration;
        }

        return response;
    }
}

/// <summary>Evicts keys after a command marked <see cref="ICacheInvalidator"/> succeeds. Innermost decorator.</summary>
sealed class CacheInvalidationCommandHandlerDecorator<TCommand, TResponse>(
    IAppCommandHandler<TCommand, TResponse> inner,
    IMemoryCache cache
) : IAppCommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> Handle(TCommand command, CancellationToken cancellationToken)
    {
        var response = await inner.Handle(command, cancellationToken);

        if (command is ICacheInvalidator invalidator)
        {
            foreach (var key in invalidator.CacheKeysToInvalidate)
            {
                cache.Remove(key);
            }
        }

        return response;
    }
}
