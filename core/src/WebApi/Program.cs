using Nona.Application;
using Nona.Infrastructure;
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

        ConfigureWebPipeline(app);
        app.Run();
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


        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddApiServices(builder.Configuration);
    }

    private static void ConfigureWebPipeline(WebApplication app)
    {
        app.MapOpenApi(); app.MapScalarApiReference(o =>
        {
            o.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                .WithTheme(ScalarTheme.Moon)
                .WithTitle("Nona config API");
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseCors("AllowFrontend");
        }
        else
        {
            app.UseCors(x => x.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapFallbackToFile("index.html");
    }
}
