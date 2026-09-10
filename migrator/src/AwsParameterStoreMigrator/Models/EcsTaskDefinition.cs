using System.Text.Json.Serialization;

namespace Nona.Migrator.AwsParameterStore.Models;

internal sealed class EcsTaskDefinitionDocument
{
    [JsonPropertyName("taskDefinition")]
    public EcsTaskDefinition? TaskDefinition { get; set; }

    [JsonPropertyName("containerDefinitions")]
    public List<EcsContainerDefinition>? ContainerDefinitions { get; set; }
}

internal sealed class EcsTaskDefinition
{
    [JsonPropertyName("containerDefinitions")]
    public List<EcsContainerDefinition>? ContainerDefinitions { get; set; }
}

internal sealed class EcsContainerDefinition
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("secrets")]
    public List<EcsSecret>? Secrets { get; set; }
}

internal sealed class EcsSecret
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("valueFrom")]
    public string? ValueFrom { get; set; }
}

internal sealed record ParameterMapping(
    string Key,
    string RequestName,
    string? Region,
    string SourceLabel);

internal sealed record TaskDefinitionMappings(
    IReadOnlyList<ParameterMapping> Parameters,
    IReadOnlyList<string> Warnings);
