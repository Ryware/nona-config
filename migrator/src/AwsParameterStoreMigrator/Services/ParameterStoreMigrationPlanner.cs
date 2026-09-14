using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Nona.Migrator.AwsParameterStore.Models;
using Nona.Migrator.Core.Models;

namespace Nona.Migrator.AwsParameterStore.Services;

internal static class ParameterStoreMigrationPlanner
{
    private const int GetParametersBatchSize = 10;

    public static async Task<MigrationPlan> BuildAsync(
        TaskDefinitionMappings mappings,
        string environment,
        string? fallbackRegion,
        ISsmClientFactory clientFactory,
        CancellationToken cancellationToken)
    {
        var warnings = new List<string>(mappings.Warnings);
        var entries = new List<PlannedConfigEntry>();
        var resolvedMappings = mappings.Parameters.Select(mapping =>
        {
            var region = mapping.Region ?? fallbackRegion;
            if (string.IsNullOrWhiteSpace(region))
                throw new InvalidOperationException(
                    $"AWS region is required for Parameter Store reference '{mapping.SourceLabel}'. " +
                    "Pass --region or configure a default AWS region.");

            return new ResolvedMapping(mapping, region);
        }).ToArray();

        foreach (var regionGroup in resolvedMappings.GroupBy(
            static mapping => mapping.Region,
            StringComparer.OrdinalIgnoreCase))
        {
            using var client = clientFactory.Create(regionGroup.Key);
            var regionMappings = regionGroup.ToArray();
            var parameters = new Dictionary<string, Parameter>(StringComparer.Ordinal);
            var requestNames = regionMappings
                .Select(static mapping => mapping.Mapping.RequestName)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            foreach (var batch in requestNames.Chunk(GetParametersBatchSize))
            {
                var response = await client.GetParametersAsync(new GetParametersRequest
                {
                    Names = batch.ToList(),
                    WithDecryption = false
                }, cancellationToken);

                if (response.InvalidParameters?.Count > 0)
                    throw new InvalidOperationException(
                        $"Parameter Store did not return: {string.Join(", ", response.InvalidParameters.Order(StringComparer.Ordinal))}.");

                AddToResponseLookup(parameters, response.Parameters ?? []);
            }

            foreach (var resolvedMapping in regionMappings)
            {
                var mapping = resolvedMapping.Mapping;
                if (!TryGetParameter(parameters, mapping.RequestName, out var parameter))
                    throw new InvalidOperationException($"Parameter Store did not return '{mapping.SourceLabel}'.");

                if (!string.Equals(parameter.Type?.Value, ParameterType.String.Value, StringComparison.Ordinal))
                {
                    warnings.Add(
                        $"Skipped key '{mapping.Key}' because Parameter Store type is '{parameter.Type?.Value ?? "unknown"}', not 'String'.");
                    continue;
                }

                if (parameter.Value is null)
                    throw new InvalidOperationException($"String parameter '{mapping.SourceLabel}' returned no value.");

                entries.Add(new PlannedConfigEntry(
                    environment,
                    mapping.Key,
                    parameter.Value,
                    "text",
                    "server",
                    mapping.SourceLabel));
            }
        }

        return new MigrationPlan(
            [environment],
            entries.OrderBy(static entry => entry.Key, StringComparer.Ordinal).ToArray(),
            warnings.Distinct(StringComparer.Ordinal).ToArray(),
            mappings.Parameters.Count);
    }

    private static void AddToResponseLookup(
        IDictionary<string, Parameter> lookup,
        IEnumerable<Parameter> parameters)
    {
        foreach (var parameter in parameters)
        {
            if (!string.IsNullOrWhiteSpace(parameter.Name))
                lookup[parameter.Name] = parameter;
            if (!string.IsNullOrWhiteSpace(parameter.ARN))
                lookup[parameter.ARN] = parameter;
        }
    }

    private static bool TryGetParameter(
        IReadOnlyDictionary<string, Parameter> parameters,
        string requestName,
        out Parameter parameter)
    {
        if (parameters.TryGetValue(requestName, out parameter!))
            return true;

        var parameterName = GetParameterNameFromArn(requestName);
        return parameterName is not null && parameters.TryGetValue(parameterName, out parameter!);
    }

    private static string? GetParameterNameFromArn(string value)
    {
        if (!value.StartsWith("arn:", StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = value.Split(':', 6);
        const string resourcePrefix = "parameter/";
        if (parts.Length != 6 || !parts[5].StartsWith(resourcePrefix, StringComparison.Ordinal))
            return null;

        return "/" + parts[5][resourcePrefix.Length..];
    }

    private sealed record ResolvedMapping(ParameterMapping Mapping, string Region);
}
