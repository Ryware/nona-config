using Nona.Application;
using Nona.Infrastructure;
using Nona.Infrastructure.Seeding;
using Nona.WebApi;
using Scalar.AspNetCore;
using Serilog;

public partial class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddUserSecrets(typeof(Program).Assembly);
        builder.Configuration.AddEnvironmentVariables();

        ConfigureServices(builder);

        var app = builder.Build();

        await SeedDataAsync(app);

        ConfigureWebPipeline(app);
        app.Run();
    }

    private static async Task SeedDataAsync(WebApplication app)
    {
        var seeder = app.Services.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync();
    }

    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddCors();

        builder.Services.AddControllers();
        builder.Services.AddOpenApi(o =>
        {
            o.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Servers = [new() { Url = "/" }];
                return Task.CompletedTask;
            });
            o.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        }
        );

        builder.Host.UseSerilog((context, services, configuration) => configuration
                  .ReadFrom.Configuration(context.Configuration)
                  .ReadFrom.Services(services)
                  .Enrich.FromLogContext()
                  .WriteTo.Console(outputTemplate: $$"""{Timestamp:u} {Timestamp:ffffff} {Level:u3} {Message:l}{NewLine}{Exception}"""));


        // builder.Services.AddApiAuthenticationServices(builder.Configuration);
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddApiServices(builder.Configuration);
    }

    private static void ConfigureWebPipeline(WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference(o =>
        {
            o.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                .WithTheme(ScalarTheme.Moon)
                .WithTitle("Nona config API");
        });

        app.UseCors(x => x.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
    }
}
