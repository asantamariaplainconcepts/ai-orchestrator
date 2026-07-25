using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.BuildingBlocks.Api;

/// <summary>
/// The one exception handler: input-validation failures become 400 ValidationProblemDetails,
/// everything else a 500 ProblemDetails. No endpoint writes its own error body.
/// </summary>
public sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        // Results.* owns the RFC 7807 rendering — status, body shape, and the
        // application/problem+json content type. Hand-writing the body loses the content type.
        if (exception is ValidationException validationException)
        {
            var failures = validationException
                .Errors.GroupBy(failure => failure.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).ToArray()
                );

            await Results.ValidationProblem(failures).ExecuteAsync(httpContext);
            return true;
        }

        LogUnhandled(logger, exception);

        await Results
            .Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Server Error",
                detail: "An unexpected error occurred."
            )
            .ExecuteAsync(httpContext);
        return true;
    }

    [LoggerMessage(EventId = 1100, Level = LogLevel.Error, Message = "Unhandled exception")]
    static partial void LogUnhandled(ILogger logger, Exception exception);
}
