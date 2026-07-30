using System.Reflection;
using AiOrchestrator.BuildingBlocks.Identity;
using FluentValidation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiOrchestrator.BuildingBlocks.CQS;

// The pipeline order is fixed by AddVsaCqsArchitecture and is not configurable per call site:
// Logging -> Authorization -> Validation -> Caching -> Handler -> InvalidateCaching.

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
/// BR-009 made mechanical (#13, design D1): the request declares what it requires, this enforces it
/// before the handler runs. Outside the caching decorator on purpose — inside it, a response cached
/// for somebody allowed would be served to somebody who is not.
/// </summary>
sealed class AuthorizationCommandHandlerDecorator<TCommand, TResponse>(
    IAppCommandHandler<TCommand, TResponse> inner,
    IProjectPermissions permissions,
    IOptions<PermissionGrants> grants
) : IAppCommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> Handle(TCommand command, CancellationToken cancellationToken)
    {
        await AuthorizationPipeline.EnsurePermitted(
            command,
            permissions,
            grants.Value,
            cancellationToken
        );
        return await inner.Handle(command, cancellationToken);
    }
}

sealed class AuthorizationQueryHandlerDecorator<TQuery, TResponse>(
    IAppQueryHandler<TQuery, TResponse> inner,
    IProjectPermissions permissions,
    IOptions<PermissionGrants> grants
) : IAppQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public async Task<TResponse> Handle(TQuery query, CancellationToken cancellationToken)
    {
        await AuthorizationPipeline.EnsurePermitted(
            query,
            permissions,
            grants.Value,
            cancellationToken
        );
        return await inner.Handle(query, cancellationToken);
    }
}

static class AuthorizationPipeline
{
    public static async Task EnsurePermitted<TRequest>(
        TRequest request,
        IProjectPermissions permissions,
        PermissionGrants grants,
        CancellationToken cancellationToken
    )
    {
        // The concrete request type, because Sender resolves the handler by it — so a declaration
        // on the command is a declaration the pipeline actually reads.
        var declared = typeof(TRequest).GetCustomAttribute<RequiresAttribute>();

        // Default deny (design D1). An operation nobody declared anything for is refused, which is
        // the whole reason this is a decorator and not a line inside each handler: forgetting has
        // to fail closed.
        if (declared is null)
        {
            throw new PermissionDeniedException(typeof(TRequest).FullName!);
        }

        // The two declarations that are not permissions. Whether anybody is asking at all is the
        // pipeline's business, not this decorator's: /api is refused unauthenticated before a
        // handler is ever resolved.
        if (declared.Access is Access.AnyCaller or Access.FiltersToCaller)
        {
            return;
        }

        if (declared.Permission is not { } permission)
        {
            // An Access value added without a rule here, or an attribute in some third state. Refused
            // rather than waved through, for the same reason omission is.
            throw new PermissionDeniedException(typeof(TRequest).FullName!);
        }

        if (request is not IScopedToProject scoped)
        {
            // Not a refusal — a wiring mistake. Refusing would hide it behind a 403 that reads like
            // an ordinary one; this says which type is wrong, and a test asserts the pairing so it
            // cannot reach a deployment (task 5.3).
            throw new InvalidOperationException(
                $"{typeof(TRequest).Name} requires '{permission}' but does not implement "
                    + $"{nameof(IScopedToProject)}, so there is no project to check it against."
            );
        }

        // Two questions, in order, and neither collapses into the other: which bundle this caller
        // holds *here*, then whether that bundle holds this permission.
        var role = await permissions.RoleOn(scoped.ProjectId, cancellationToken);

        if (role is null || !grants.Holds(role.Value, permission))
        {
            throw new PermissionDeniedException(typeof(TRequest).FullName!);
        }
    }
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
