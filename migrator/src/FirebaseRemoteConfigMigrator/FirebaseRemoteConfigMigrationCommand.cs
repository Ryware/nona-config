using Microsoft.Kiota.Http.HttpClientLibrary;
using Nona.Migrator.Core;
using Nona.Migrator.Core.Generated;
using Nona.Migrator.Core.Generated.Models;
using Nona.Migrator.Core.Models;
using Nona.Migrator.Core.Services;
using Nona.Migrator.FirebaseRemoteConfig.Service;

namespace Nona.Migrator.FirebaseRemoteConfig;

public static class FirebaseRemoteConfigMigrationCommand
{
    public static async Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken,
        TextWriter? output = null,
        TextWriter? error = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;

        try
        {
            var configuration = await MigrationConfiguration.LoadAsync(args, cancellationToken);
            configuration.Validate();

            using var nonaHttpClient = new HttpClient();

            var firebaseClient = new FirebaseRemoteConfigClient(configuration.Firebase);
            var sourcePlans = new List<MigrationPlan>();

            foreach (var source in configuration.Firebase.GetImportSources())
            {
                var template = await firebaseClient.GetTemplateAsync(source, cancellationToken);
                var sourcePlan = MigrationPlanner.Build(template, configuration.Migration, source.Scope);
                sourcePlans.Add(sourcePlan);
            }

            var plan = MigrationPlanMerger.Merge(sourcePlans, configuration.Migration.RenameConflictingKeys);

            await output.WriteLineAsync($"Loaded Firebase template. Params={plan.ParameterCount}, Envs={plan.Environments.Count}, Ops={plan.Entries.Count}.");

            foreach (var warning in plan.Warnings)
                await output.WriteLineAsync($"WARN: {warning}");

            if (plan.Entries.Count == 0)
            {
                await output.WriteLineAsync("Nothing to migrate. Check env map/default env config.");
                return 0;
            }

            if (configuration.Migration.DryRun)
            {
                await output.WriteLineAsync("Dry run on. Planned writes:");
                foreach (var entry in plan.Entries)
                    await output.WriteLineAsync($" - [{entry.Environment}] {entry.Key} ({entry.ContentType}) <= {entry.SourceLabel}");

                return 0;
            }

            var authProvider = new NonaAuthenticationProvider(configuration.Nona);
            var adapter = new HttpClientRequestAdapter(authProvider, httpClient: nonaHttpClient)
            {
                BaseUrl = configuration.Nona.BaseUrl.TrimEnd('/')
            };
            var nonaClient = new NonaMigrationApiClient(adapter);

            var projectName = await EnsureProjectAsync(nonaClient, configuration.Nona.ProjectName, cancellationToken);

            foreach (var environment in plan.Environments)
                await EnsureEnvironmentAsync(nonaClient, projectName, environment, cancellationToken);

            foreach (var entry in plan.Entries)
            {
                await nonaClient.Admin.Projects[projectName]
                    .Environments[entry.Environment].ConfigEntries[entry.Key]
                    .PutAsync(new UpsertConfigEntryRequest
                    {
                        Value = entry.Value,
                        ContentType = entry.ContentType,
                        Scope = entry.Scope
                    }, cancellationToken: cancellationToken);

                await output.WriteLineAsync(
                    $"Migrated [{entry.Environment}] {entry.Key} ({entry.Scope}, {entry.ContentType}) <= {entry.SourceLabel}");
            }

            await output.WriteLineAsync("Migration complete.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("Cancelled.");
            return 2;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    private static async Task<string> EnsureProjectAsync(
        NonaMigrationApiClient client, string projectName, CancellationToken ct)
    {
        var projects = await client.Admin.Projects.GetAsync(cancellationToken: ct);
        var existing = projects?.FirstOrDefault(p =>
            string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
            return existing.Name!;

        var created = await client.Admin.Projects
            .PostAsync(new CreateProjectRequest { Name = projectName }, cancellationToken: ct);

        return created!.Name!;
    }

    private static async Task EnsureEnvironmentAsync(
        NonaMigrationApiClient client, string projectName, string environmentName, CancellationToken ct)
    {
        var environments = await client.Admin.Projects[projectName].Environments
            .GetAsync(cancellationToken: ct);

        if (environments?.Any(e =>
            string.Equals(e.Name, environmentName, StringComparison.OrdinalIgnoreCase)) == true)
            return;

        await client.Admin.Projects[projectName].Environments
            .PostAsync(new CreateEnvironmentRequest { Name = environmentName }, cancellationToken: ct);
    }
}
