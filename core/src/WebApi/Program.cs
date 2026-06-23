using Nona.Application;
using Nona.Infrastructure;
using Nona.Infrastructure.Configuration;
using Nona.WebApi;
using Nona.WebApi.Endpoints;
using Nona.WebApi.Serialization;

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
                .WithExposedHeaders(NonaResponseHeaders.LogicalContentType)
                .AllowCredentials());
        });

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, NonaJsonSerializerContext.Default);
        });

        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddApiServices(builder.Configuration);
    }

    private static void ConfigureWebPipeline(WebApplication app)
    {
        app.UseCors(CorsPolicyName);

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapNonaEndpoints();
        app.MapFallbackToFile("index.html");
    }
}
