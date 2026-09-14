using Nona.Migrator.AwsParameterStore.Services;
using Nona.Migrator.Core.Models;
using Nona.Migrator.Core.Services;

namespace Nona.Migrator.AwsParameterStore;

public static class AwsParameterStoreMigrationCommand
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
            var configuration = ParameterStoreMigrationConfiguration.Parse(args);
            return await RunAsync(
                configuration,
                new SsmClientFactory(configuration.Profile),
                new NonaMigrationWriter(),
                cancellationToken,
                output,
                error);
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync(DescribeException(exception));
            return 1;
        }
    }

    internal static async Task<int> RunAsync(
        ParameterStoreMigrationConfiguration configuration,
        ISsmClientFactory clientFactory,
        INonaMigrationWriter writer,
        CancellationToken cancellationToken,
        TextWriter output,
        TextWriter error)
    {
        try
        {
            var mappings = await EcsTaskDefinitionParser.LoadAsync(
                configuration.TaskDefinitionPath,
                cancellationToken);
            var fallbackRegion = configuration.Region ?? clientFactory.ResolveDefaultRegion();
            var plan = await ParameterStoreMigrationPlanner.BuildAsync(
                mappings,
                configuration.EnvironmentName,
                fallbackRegion,
                clientFactory,
                cancellationToken);

            await output.WriteLineAsync(
                $"Loaded ECS task definition. Params={plan.ParameterCount}, Envs={plan.Environments.Count}, Ops={plan.Entries.Count}.");

            foreach (var warning in plan.Warnings)
                await output.WriteLineAsync($"WARN: {warning}");

            if (plan.Entries.Count == 0)
            {
                await output.WriteLineAsync("Nothing to migrate. No referenced Parameter Store values have type String.");
                return 0;
            }

            if (configuration.DryRun)
            {
                await output.WriteLineAsync("Dry run on. Planned writes:");
                foreach (var entry in plan.Entries)
                    await WritePlannedEntryAsync(output, entry);
                return 0;
            }

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

    private static Task WritePlannedEntryAsync(TextWriter output, PlannedConfigEntry entry)
        => output.WriteLineAsync(
            $" - [{entry.Environment}] {entry.Key} ({entry.Scope}, {entry.ContentType}) <= {entry.SourceLabel}");

}
