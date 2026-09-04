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

            var writer = new NonaMigrationWriter();
            await writer.ApplyAsync(
                configuration.Nona,
                plan,
                entry => output.WriteLineAsync(
                    $"Migrated [{entry.Environment}] {entry.Key} ({entry.Scope}, {entry.ContentType}) <= {entry.SourceLabel}"),
                cancellationToken);

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
            await error.WriteLineAsync(DescribeException(exception));
            return 1;
        }
    }

    internal static string DescribeException(Exception exception)
        => exception is Microsoft.Kiota.Abstractions.ApiException apiException
            ? NonaApiExceptionFormatter.Format(apiException)
            : $"Error: {TerminalSafeText.SingleLine(exception.Message)}";

}
