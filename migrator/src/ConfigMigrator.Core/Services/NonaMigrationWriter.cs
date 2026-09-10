using Microsoft.Kiota.Http.HttpClientLibrary;
using Nona.Migrator.Core.Generated;
using Nona.Migrator.Core.Generated.Models;
using Nona.Migrator.Core.Models;
using Nona.Migrator.Core.Options;

namespace Nona.Migrator.Core.Services;

public interface INonaMigrationWriter
{
    Task ApplyAsync(
        NonaOptions options,
        MigrationPlan plan,
        Func<PlannedConfigEntry, Task>? onEntryMigrated,
        CancellationToken cancellationToken);
}

public sealed class NonaMigrationWriter : INonaMigrationWriter
{
    public async Task ApplyAsync(
        NonaOptions options,
        MigrationPlan plan,
        Func<PlannedConfigEntry, Task>? onEntryMigrated,
        CancellationToken cancellationToken)
    {
        using var httpClient = NonaApiHttpClientFactory.Create();
        var authProvider = new NonaAuthenticationProvider(options);
        var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient)
        {
            BaseUrl = options.BaseUrl.TrimEnd('/')
        };
        var client = new NonaMigrationApiClient(adapter);

        var projectName = await EnsureProjectAsync(client, options.ProjectName, cancellationToken);

        foreach (var environment in plan.Environments)
            await EnsureEnvironmentAsync(client, projectName, environment, cancellationToken);

        foreach (var entry in plan.Entries)
        {
            await client.Admin.Projects[projectName]
                .Environments[entry.Environment].ConfigEntries[entry.Key]
                .PutAsync(new UpsertConfigEntryRequest
                {
                    Value = entry.Value,
                    ContentType = entry.ContentType,
                    Scope = entry.Scope
                }, cancellationToken: cancellationToken);

            if (onEntryMigrated is not null)
                await onEntryMigrated(entry);
        }
    }

    private static async Task<string> EnsureProjectAsync(
        NonaMigrationApiClient client,
        string projectName,
        CancellationToken cancellationToken)
    {
        var projects = await client.Admin.Projects.GetAsync(cancellationToken: cancellationToken);
        var existing = projects?.FirstOrDefault(project =>
            string.Equals(project.Name, projectName, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
            return existing.Name!;

        var created = await client.Admin.Projects.PostAsync(
            new CreateProjectRequest { Name = projectName },
            cancellationToken: cancellationToken);

        return created!.Name!;
    }

    private static async Task EnsureEnvironmentAsync(
        NonaMigrationApiClient client,
        string projectName,
        string environmentName,
        CancellationToken cancellationToken)
    {
        var environments = await client.Admin.Projects[projectName].Environments
            .GetAsync(cancellationToken: cancellationToken);

        if (environments?.Any(environment =>
            string.Equals(environment.Name, environmentName, StringComparison.OrdinalIgnoreCase)) == true)
            return;

        await client.Admin.Projects[projectName].Environments.PostAsync(
            new CreateEnvironmentRequest { Name = environmentName },
            cancellationToken: cancellationToken);
    }
}
