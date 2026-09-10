using System.Text.Json;
using Nona.Migrator.AwsParameterStore.Models;

namespace Nona.Migrator.AwsParameterStore.Services;

internal static class EcsTaskDefinitionParser
{
    public static async Task<TaskDefinitionMappings> LoadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync(
            stream,
            AwsParameterStoreSerializerContext.Default.EcsTaskDefinitionDocument,
            cancellationToken);

        if (document is null)
            throw new InvalidOperationException("Task definition JSON is empty.");

        var hasRawShape = document.ContainerDefinitions is not null;
        var hasWrappedShape = document.TaskDefinition?.ContainerDefinitions is not null;

        if (hasRawShape && hasWrappedShape)
            throw new InvalidOperationException(
                "Task definition JSON contains containerDefinitions in both the root and taskDefinition envelope.");

        var taskDefinition = hasWrappedShape
            ? document.TaskDefinition!
            : hasRawShape
                ? new EcsTaskDefinition { ContainerDefinitions = document.ContainerDefinitions }
                : throw new InvalidOperationException(
                    "Task definition JSON must contain containerDefinitions at the root or under taskDefinition.");

        return Parse(taskDefinition);
    }

    internal static TaskDefinitionMappings Parse(EcsTaskDefinition taskDefinition)
    {
        var warnings = new List<string>();
        var mappings = new Dictionary<string, ParameterMapping>(StringComparer.OrdinalIgnoreCase);

        foreach (var container in taskDefinition.ContainerDefinitions ?? [])
        {
            foreach (var secret in container.Secrets ?? [])
            {
                var key = secret.Name?.Trim();
                var reference = secret.ValueFrom?.Trim();
                var containerName = string.IsNullOrWhiteSpace(container.Name) ? "unnamed" : container.Name;

                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(reference))
                    throw new InvalidOperationException($"Container '{containerName}' has a secret mapping with a missing name or valueFrom.");

                var parsed = ParseReference(key, reference, containerName, warnings);
                if (parsed is null)
                    continue;

                if (!IsValidNonaKey(key))
                    throw new InvalidOperationException($"ECS secret name '{key}' is not a valid Nona key.");

                if (!mappings.TryGetValue(key, out var existing))
                {
                    mappings[key] = parsed;
                    continue;
                }

                if (string.Equals(existing.RequestName, parsed.RequestName, StringComparison.Ordinal)
                    && string.Equals(existing.Region, parsed.Region, StringComparison.OrdinalIgnoreCase))
                    continue;

                throw new InvalidOperationException(
                    $"ECS secret name '{key}' maps to multiple Parameter Store parameters: " +
                    $"'{existing.SourceLabel}' and '{parsed.SourceLabel}'.");
            }
        }

        return new TaskDefinitionMappings(
            mappings.Values.OrderBy(static mapping => mapping.Key, StringComparer.Ordinal).ToArray(),
            warnings);
    }

    private static ParameterMapping? ParseReference(
        string key,
        string reference,
        string containerName,
        ICollection<string> warnings)
    {
        if (!reference.StartsWith("arn:", StringComparison.OrdinalIgnoreCase))
            return new ParameterMapping(key, reference, null, reference);

        var parts = reference.Split(':', 6);
        if (parts.Length != 6 || string.IsNullOrWhiteSpace(parts[2]))
            throw new InvalidOperationException($"ECS secret '{key}' has a malformed ARN in valueFrom.");

        var service = parts[2];
        if (service.Equals("secretsmanager", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"Skipped Secrets Manager reference for key '{key}' in container '{containerName}'.");
            return null;
        }

        if (!service.Equals("ssm", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"ECS secret '{key}' references unsupported ARN service '{service}'.");

        var region = parts[3];
        var resource = parts[5];
        if (string.IsNullOrWhiteSpace(region)
            || !resource.StartsWith("parameter/", StringComparison.Ordinal)
            || resource.Length == "parameter/".Length)
            throw new InvalidOperationException($"ECS secret '{key}' has a malformed SSM parameter ARN.");

        return new ParameterMapping(key, reference, region, reference);
    }

    private static bool IsValidNonaKey(string key)
    {
        var hasAlphaNumeric = false;
        foreach (var character in key)
        {
            if (character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                hasAlphaNumeric = true;
                continue;
            }

            if (character is not (':' or '.' or '_' or '-'))
                return false;
        }

        if (!hasAlphaNumeric)
            return false;

        var segments = key.Split(':');
        return segments.Length <= 4 && segments.All(static segment => segment.Length > 0);
    }
}
