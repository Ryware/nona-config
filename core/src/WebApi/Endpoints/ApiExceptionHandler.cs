using Microsoft.AspNetCore.Diagnostics;

namespace Nona.WebApi.Endpoints;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        await ApiProblemResults.InternalServerError().ExecuteAsync(httpContext);
        return true;
    }
}
