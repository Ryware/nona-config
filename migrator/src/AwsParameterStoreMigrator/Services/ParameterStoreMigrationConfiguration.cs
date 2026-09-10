using Nona.Migrator.Core.Options;

namespace Nona.Migrator.AwsParameterStore.Services;

internal sealed record ParameterStoreMigrationConfiguration(
    string TaskDefinitionPath,
    string EnvironmentName,
    string? Region,
    string? Profile,
    bool DryRun,
    NonaOptions Nona)
{
    public static ParameterStoreMigrationConfiguration Parse(string[] args)
    {
        string? taskDefinitionPath = null;
        string? environmentName = null;
        string? region = null;
        string? profile = null;
        string? baseUrl = null;
        string? projectName = null;
        string? email = null;
        string? password = null;
        string? bearerToken = null;
        var dryRun = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                dryRun = true;
                continue;
            }

            var value = ReadValue(args, ref index, argument);
            switch (argument.ToLowerInvariant())
            {
                case "--task-definition": taskDefinitionPath = value; break;
                case "--environment": environmentName = value; break;
                case "--region": region = value; break;
                case "--profile": profile = value; break;
                case "--base-url":
                case "--api-url": baseUrl = value; break;
                case "--project":
                case "--project-name": projectName = value; break;
                case "--token":
                case "--bearer-token": bearerToken = value; break;
                case "--email": email = value; break;
                case "--password": password = value; break;
                default: throw new InvalidOperationException($"Unknown option '{argument}'.");
            }
        }

        var configuration = new ParameterStoreMigrationConfiguration(
            taskDefinitionPath ?? string.Empty,
            environmentName ?? string.Empty,
            region,
            profile,
            dryRun,
            new NonaOptions
            {
                BaseUrl = baseUrl ?? string.Empty,
                ProjectName = projectName ?? string.Empty,
                Email = email,
                Password = password,
                BearerToken = bearerToken
            });
        configuration.Validate();
        return configuration;
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(TaskDefinitionPath))
            throw new InvalidOperationException("--task-definition is required.");
        if (!File.Exists(TaskDefinitionPath))
            throw new FileNotFoundException("Task definition file was not found.", TaskDefinitionPath);
        if (string.IsNullOrWhiteSpace(EnvironmentName))
            throw new InvalidOperationException("--environment is required.");
        if (string.IsNullOrWhiteSpace(Nona.BaseUrl))
            throw new InvalidOperationException("Nona base URL is required.");
        if (string.IsNullOrWhiteSpace(Nona.ProjectName))
            throw new InvalidOperationException("Nona project name is required.");

        var hasBearerToken = !string.IsNullOrWhiteSpace(Nona.BearerToken);
        var hasEmailPassword = !string.IsNullOrWhiteSpace(Nona.Email) && !string.IsNullOrWhiteSpace(Nona.Password);
        if (!hasBearerToken && !hasEmailPassword)
            throw new InvalidOperationException("Set Nona bearer token or email/password.");
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new InvalidOperationException($"Option '{option}' requires a value.");

        return args[++index];
    }
}
