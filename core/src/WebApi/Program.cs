using Nona.Application;
using Nona.Infrastructure;
using Nona.Infrastructure.Configuration;
using Nona.WebApi;
using Nona.WebApi.Endpoints;
using Nona.WebApi.Serialization;
using Scalar.AspNetCore;

public partial class Program
{
    private const string CorsPolicyName = "AllowAllOrigins";

    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddUserSecrets(typeof(Program).Assembly);
        PersistentJwtConfiguration.Apply(builder.Configuration);
        builder.Configuration.AddEnvironmentVariables();

        ConfigureServices(builder);

        var app = builder.Build();

        ConfigureWebPipeline(app);
        app.Run();
    }


    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy => policy
                .SetIsOriginAllowed(_ => true)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders(
                    NonaResponseHeaders.LogicalContentType,
                    NonaResponseHeaders.EntityTag)
                .AllowCredentials());
        });

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, NonaJsonSerializerContext.Default);
        });

        builder.Services.AddOpenApi();

        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddApiServices(builder.Configuration);
    }

    private static void ConfigureWebPipeline(WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                .WithTheme(ScalarTheme.Moon)
                .WithTitle("Nona config API");
        });

        app.UseCors(CorsPolicyName);
        app.UseExceptionHandler();

        app.UseNonaSpaStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapNonaEndpoints();
        app.MapNonaBackendFallbacks();
        app.MapFallbackToFile("index.html");
    }
}
