using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Nona.Application.Common;
using Nona.WebApi;
using System.Net.Http;

internal sealed class ConfigValueOpenApiTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components?.Schemas?.Remove("ConfigValueResponse");

        if (document.Paths is null
            || !document.Paths.TryGetValue("/api/{environmentId}/{key}", out var pathItem)
            || pathItem.Operations is null
            || !pathItem.Operations.TryGetValue(HttpMethod.Get, out var operation)
            || operation.Responses is null
            || !operation.Responses.TryGetValue("200", out var response))
        {
            return Task.CompletedTask;
        }

        var rawResponse = new OpenApiResponse
        {
            Description = response.Description ?? "OK",
            Headers = new Dictionary<string, IOpenApiHeader>(),
            Content = new Dictionary<string, OpenApiMediaType>()
        };

        rawResponse.Headers[NonaResponseHeaders.LogicalContentType] = new OpenApiHeader
        {
            Description = "Logical Nona value type such as json, text, number, or boolean.",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String
            }
        };

        rawResponse.Content["application/json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = $"Raw config value. Interpret using the {NonaResponseHeaders.LogicalContentType} response header."
            }
        };

        operation.Responses["200"] = rawResponse;

        return Task.CompletedTask;
    }
}
