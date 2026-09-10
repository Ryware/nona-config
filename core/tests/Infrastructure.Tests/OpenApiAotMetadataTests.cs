using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Nona.Infrastructure.Tests;

public class OpenApiAotMetadataTests
{
    [Test]
    public async Task OpenApi_WithGeneratedJsonMetadata_DocumentsNullableDateOnly()
    {
        await using var app = await StartAppAsync();
        using var response = await app.GetTestClient().GetAsync("/openapi/v1.json");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var parameters = document.RootElement
            .GetProperty("paths")
            .GetProperty("/audit-logs")
            .GetProperty("get")
            .GetProperty("parameters");

        await Assert.That(parameters.GetArrayLength()).IsEqualTo(1);
        await Assert.That(parameters[0].GetProperty("name").GetString()).IsEqualTo("dateFrom");
        await Assert.That(parameters[0].GetProperty("schema").GetProperty("format").GetString())
            .IsEqualTo("date");
    }

    private static async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Clear();
            options.SerializerOptions.TypeInfoResolverChain.Add(GetNonaJsonSerializerContext());
        });
        builder.Services.AddOpenApi();

        var app = builder.Build();
        app.MapOpenApi();
        app.MapGet("/audit-logs", (DateOnly? dateFrom) => Results.NoContent());
        await app.StartAsync();
        return app;
    }

    private static JsonSerializerContext GetNonaJsonSerializerContext()
    {
        var contextType = typeof(Program).Assembly.GetType(
            "Nona.WebApi.Serialization.NonaJsonSerializerContext",
            throwOnError: true)!;
        var defaultProperty = contextType.GetProperty(
            "Default",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Nona JSON serializer context has no Default property.");

        return (JsonSerializerContext)(defaultProperty.GetValue(null)
            ?? throw new InvalidOperationException("Nona JSON serializer context Default is null."));
    }
}
