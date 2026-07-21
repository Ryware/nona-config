namespace Nona.WebApi.Endpoints;

public static class NonaFallbackRouteBuilderExtensions
{
    private static readonly PathString[] BackendPrefixes =
    [
        new("/api"),
        new("/admin"),
        new("/auth"),
        new("/public")
    ];

    public static IApplicationBuilder UseNonaSpaStaticFiles(this IApplicationBuilder app)
    {
        app.UseWhen(
            context => !IsBackendPath(context.Request.Path),
            spa =>
            {
                spa.UseDefaultFiles();
                spa.UseStaticFiles();
            });

        return app;
    }

    public static IEndpointRouteBuilder MapNonaBackendFallbacks(this IEndpointRouteBuilder app)
    {
        app.Map("/api/{**path}", NotFoundAsync)
            .ExcludeFromDescription();
        app.Map("/admin/{**path}", NotFoundAsync)
            .RequireAuthorization()
            .ExcludeFromDescription();
        app.Map("/auth/{**path}", NotFoundAsync)
            .ExcludeFromDescription();
        app.Map("/public/{**path}", NotFoundAsync)
            .ExcludeFromDescription();

        return app;
    }

    private static bool IsBackendPath(PathString path)
        => BackendPrefixes.Any(prefix => path.StartsWithSegments(prefix));

    private static Task NotFoundAsync(HttpContext httpContext)
        => ApiProblemResults
            .NotFound("Endpoint not found.")
            .ExecuteAsync(httpContext);
}
