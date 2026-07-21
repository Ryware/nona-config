using FluentValidation.Results;
using Nona.WebApi.Serialization;

namespace Nona.WebApi.Endpoints;

internal sealed record ApiProblemDetails(
    string Type,
    string Title,
    int Status,
    string Detail,
    string Instance,
    string? ErrorCode = null);

internal sealed record ApiValidationProblemDetails(
    string Type,
    string Title,
    int Status,
    string Detail,
    string Instance,
    IReadOnlyDictionary<string, string[]> Errors,
    string? ErrorCode = null);

internal static class ApiProblemResults
{
    private const string Rfc9110 = "https://tools.ietf.org/html/rfc9110";

    public static IResult BadRequest(string detail, string? errorCode = null)
        => Problem(StatusCodes.Status400BadRequest, "Bad Request", "15.5.1", detail, errorCode);

    public static IResult Unauthorized(string detail, string? errorCode = null)
        => Problem(StatusCodes.Status401Unauthorized, "Unauthorized", "15.5.2", detail, errorCode);

    public static IResult Forbidden(string detail, string? errorCode = null)
        => Problem(StatusCodes.Status403Forbidden, "Forbidden", "15.5.4", detail, errorCode);

    public static IResult NotFound(string detail, string? errorCode = null)
        => Problem(StatusCodes.Status404NotFound, "Not Found", "15.5.5", detail, errorCode);

    public static IResult Conflict(string detail, string? errorCode = null)
        => Problem(StatusCodes.Status409Conflict, "Conflict", "15.5.10", detail, errorCode);

    public static IResult Gone(string detail, string? errorCode = null)
        => Problem(StatusCodes.Status410Gone, "Gone", "15.5.11", detail, errorCode);

    public static IResult InternalServerError()
        => Problem(
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            "15.6.1",
            "An unexpected error occurred.");

    public static IResult Validation(IEnumerable<ValidationFailure> failures)
    {
        var errors = failures
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        return new ApiValidationProblemResult(errors);
    }

    private static IResult Problem(
        int status,
        string title,
        string section,
        string detail,
        string? errorCode = null)
        => new ApiProblemResult(status, title, $"{Rfc9110}#section-{section}", detail, errorCode);

    private sealed record ApiProblemResult(
        int Status,
        string Title,
        string Type,
        string Detail,
        string? ErrorCode) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            var problem = new ApiProblemDetails(
                Type,
                Title,
                Status,
                Detail,
                Instance(httpContext),
                ErrorCode);

            return Results.Json(
                    problem,
                    NonaJsonSerializerContext.Default.ApiProblemDetails,
                    statusCode: Status,
                    contentType: "application/problem+json")
                .ExecuteAsync(httpContext);
        }
    }

    private sealed record ApiValidationProblemResult(
        IReadOnlyDictionary<string, string[]> Errors) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            var problem = new ApiValidationProblemDetails(
                $"{Rfc9110}#section-15.5.1",
                "One or more validation errors occurred.",
                StatusCodes.Status400BadRequest,
                "One or more validation errors occurred.",
                Instance(httpContext),
                Errors);

            return Results.Json(
                    problem,
                    NonaJsonSerializerContext.Default.ApiValidationProblemDetails,
                    statusCode: StatusCodes.Status400BadRequest,
                    contentType: "application/problem+json")
                .ExecuteAsync(httpContext);
        }
    }

    private static string Instance(HttpContext httpContext)
        => $"{httpContext.Request.PathBase}{httpContext.Request.Path}";
}
