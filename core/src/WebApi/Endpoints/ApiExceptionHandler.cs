using Microsoft.AspNetCore.Diagnostics;

namespace Nona.WebApi.Endpoints;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception while processing an HTTP request.");

        await ApiProblemResults.InternalServerError().ExecuteAsync(httpContext);
        return true;
    }
}
