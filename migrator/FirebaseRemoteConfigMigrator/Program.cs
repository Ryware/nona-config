using Nona.FirebaseRemoteConfigMigrator.Models;

namespace Nona.FirebaseRemoteConfigMigrator;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellationTokenSource.Cancel();
            };

            var configuration = await MigrationConfiguration.LoadAsync(args, cancellationTokenSource.Token);
            configuration.Validate();

            using var nonaHttpClient = new HttpClient();

            var firebaseClient = new FirebaseRemoteConfigClient(configuration.Firebase);
            var sourcePlans = new List<MigrationPlan>();

            foreach (var source in configuration.Firebase.GetImportSources())
            {
                var template = await firebaseClient.GetTemplateAsync(source, cancellationTokenSource.Token);
                var sourcePlan = MigrationPlanner.Build(template, configuration.Migration, source.Scope);
                sourcePlans.Add(sourcePlan);
            }

            var plan = MigrationPlanMerger.Merge(sourcePlans, configuration.Migration.RenameConflictingKeys);

            Console.WriteLine($"Loaded Firebase template. Params={plan.ParameterCount}, Envs={plan.Environments.Count}, Ops={plan.Entries.Count}.");

            foreach (var warning in plan.Warnings)
                Console.WriteLine($"WARN: {warning}");

            if (plan.Entries.Count == 0)
            {
                Console.WriteLine("Nothing to migrate. Check env map/default env config.");
                return 0;
            }

            if (configuration.Migration.DryRun)
            {
                Console.WriteLine("Dry run on. Planned writes:");
                foreach (var entry in plan.Entries)
                    Console.WriteLine($" - [{entry.Environment}] {entry.Key} ({entry.ContentType}) <= {entry.SourceLabel}");

                return 0;
            }

            var nonaClient = new NonaAdminClient(nonaHttpClient, configuration.Nona);
            var projectName = await nonaClient.EnsureProjectAsync(configuration.Nona.ProjectName, cancellationTokenSource.Token);

            foreach (var environment in plan.Environments)
                await nonaClient.EnsureEnvironmentAsync(projectName, environment, cancellationTokenSource.Token);

            foreach (var entry in plan.Entries)
            {
                await nonaClient.UpsertConfigEntryAsync(
                    projectName,
                    entry.Environment,
                    entry.Key,
                    new UpsertConfigEntryRequest(entry.Value, entry.ContentType, entry.Scope),
                    cancellationTokenSource.Token);

                Console.WriteLine($"Migrated [{entry.Environment}] {entry.Key} ({entry.Scope}, {entry.ContentType}) <= {entry.SourceLabel}");
            }

            Console.WriteLine("Migration complete.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
