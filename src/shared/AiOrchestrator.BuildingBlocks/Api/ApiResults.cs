using ErrorOr;
using Microsoft.AspNetCore.Http;

namespace AiOrchestrator.BuildingBlocks.Api;

/// <summary>
/// The single mapping from domain errors to HTTP. Every failing endpoint returns RFC 7807
/// ProblemDetails through here — no ad-hoc status codes at call sites.
/// </summary>
public static class ApiResults
{
    public static IResult Problem(List<Error> errors)
    {
        if (errors.Count == 0)
        {
            return Results.Problem();
        }

        if (errors.TrueForAll(error => error.Type == ErrorType.Validation))
        {
            return ValidationProblem(errors);
        }

        return Problem(errors[0]);
    }

    public static IResult Problem(Error error) =>
        Results.Problem(
            statusCode: StatusCode(error.Type),
            title: TitleFor(error.Type),
            detail: error.Description,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code }
        );

    static IResult ValidationProblem(List<Error> errors)
    {
        var failures = errors
            .GroupBy(error => error.Code)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray()
            );

        return Results.ValidationProblem(failures);
    }

    static int StatusCode(ErrorType type) =>
        type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

    static string TitleFor(ErrorType type) =>
        type switch
        {
            ErrorType.Validation => "Bad Request",
            ErrorType.Unauthorized => "Unauthorized",
            ErrorType.Forbidden => "Forbidden",
            ErrorType.NotFound => "Not Found",
            ErrorType.Conflict => "Conflict",
            _ => "Server Error",
        };
}
