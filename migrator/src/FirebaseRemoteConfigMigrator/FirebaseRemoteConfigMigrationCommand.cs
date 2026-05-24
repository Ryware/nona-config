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

            var nonaClient = new NonaAdminClient(nonaHttpClient, configuration.Nona);
            var projectName = await nonaClient.EnsureProjectAsync(configuration.Nona.ProjectName, cancellationToken);

            foreach (var environment in plan.Environments)
                await nonaClient.EnsureEnvironmentAsync(projectName, environment, cancellationToken);

            foreach (var entry in plan.Entries)
            {
                await nonaClient.UpsertConfigEntryAsync(
                    projectName,
                    entry.Environment,
                    entry.Key,
                    new UpsertConfigEntryRequest(entry.Value, entry.ContentType, entry.Scope),
                    cancellationToken);

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
}
