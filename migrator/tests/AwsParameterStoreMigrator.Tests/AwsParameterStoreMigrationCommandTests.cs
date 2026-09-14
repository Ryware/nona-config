using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Nona.Migrator.AwsParameterStore.Services;
using Nona.Migrator.Core.Models;
using Nona.Migrator.Core.Options;
using Nona.Migrator.Core.Services;
using NSubstitute;

namespace Nona.Migrator.AwsParameterStore.Tests;

public sealed class AwsParameterStoreMigrationCommandTests
{
    [Test]
    public async Task DryRun_DoesNotWriteOrExposeParameterValue()
    {
        using var taskDefinition = new TempTaskDefinition("""
            {
              "containerDefinitions": [
                { "name": "api", "secrets": [ { "name": "API_KEY", "valueFrom": "/app/api-key" } ] }
              ]
            }
            """);
        var client = Substitute.For<IAmazonSimpleSystemsManagement>();
        client.GetParametersAsync(Arg.Any<GetParametersRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetParametersResponse
            {
                Parameters = [new Parameter { Name = "/app/api-key", Type = ParameterType.String, Value = "super-secret-value" }]
            });
        var factory = new SingleClientFactory(client, "eu-central-1");
        var writer = new CapturingWriter();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await AwsParameterStoreMigrationCommand.RunAsync(
            Configuration(taskDefinition.Path, dryRun: true),
            factory,
            writer,
            CancellationToken.None,
            output,
            error);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(writer.ApplyCount).IsEqualTo(0);
        await Assert.That(output.ToString()).Contains("API_KEY");
        await Assert.That(output.ToString()).DoesNotContain("super-secret-value");
        await Assert.That(error.ToString()).IsEmpty();
    }

    [Test]
    public async Task Execute_WritesCompletePlanAfterSourceLoad()
    {
        using var taskDefinition = new TempTaskDefinition("""
            { "containerDefinitions": [ { "secrets": [ { "name": "PLAIN_KEY", "valueFrom": "/app/plain" } ] } ] }
            """);
        var client = Substitute.For<IAmazonSimpleSystemsManagement>();
        client.GetParametersAsync(Arg.Any<GetParametersRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetParametersResponse
            {
                Parameters = [new Parameter { Name = "/app/plain", Type = ParameterType.String, Value = "plain-value" }]
            });
        var writer = new CapturingWriter();

        var exitCode = await AwsParameterStoreMigrationCommand.RunAsync(
            Configuration(taskDefinition.Path, dryRun: false),
            new SingleClientFactory(client, "eu-central-1"),
            writer,
            CancellationToken.None,
            new StringWriter(),
            new StringWriter());

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(writer.ApplyCount).IsEqualTo(1);
        await Assert.That(writer.Plan!.Entries.Single().Key).IsEqualTo("PLAIN_KEY");
    }

    [Test]
    public async Task MissingParameter_AbortsBeforeNonaWrites()
    {
        using var taskDefinition = new TempTaskDefinition("""
            { "containerDefinitions": [ { "secrets": [ { "name": "MISSING", "valueFrom": "/app/missing" } ] } ] }
            """);
        var client = Substitute.For<IAmazonSimpleSystemsManagement>();
        client.GetParametersAsync(Arg.Any<GetParametersRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetParametersResponse { InvalidParameters = ["/app/missing"] });
        var writer = new CapturingWriter();
        var error = new StringWriter();

        var exitCode = await AwsParameterStoreMigrationCommand.RunAsync(
            Configuration(taskDefinition.Path, dryRun: false),
            new SingleClientFactory(client, "eu-central-1"),
            writer,
            CancellationToken.None,
            new StringWriter(),
            error);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(writer.ApplyCount).IsEqualTo(0);
        await Assert.That(error.ToString()).Contains("did not return");
    }

    private static ParameterStoreMigrationConfiguration Configuration(string path, bool dryRun)
        => new(
            path,
            "prod",
            "eu-central-1",
            null,
            dryRun,
            new NonaOptions
            {
                BaseUrl = "https://nona.example.com",
                ProjectName = "cms",
                BearerToken = "token"
            });

    private sealed class CapturingWriter : INonaMigrationWriter
    {
        public int ApplyCount { get; private set; }
        public MigrationPlan? Plan { get; private set; }

        public async Task ApplyAsync(
            NonaOptions options,
            MigrationPlan plan,
            Func<PlannedConfigEntry, Task>? onEntryMigrated,
            CancellationToken cancellationToken)
        {
            ApplyCount++;
            Plan = plan;
            if (onEntryMigrated is not null)
            {
                foreach (var entry in plan.Entries)
                    await onEntryMigrated(entry);
            }
        }
    }

    private sealed class SingleClientFactory(
        IAmazonSimpleSystemsManagement client,
        string defaultRegion) : ISsmClientFactory
    {
        public string? ResolveDefaultRegion() => defaultRegion;
        public IAmazonSimpleSystemsManagement Create(string region) => client;
    }

    private sealed class TempTaskDefinition : IDisposable
    {
        public string Path { get; } = System.IO.Path.GetTempFileName();

        public TempTaskDefinition(string json) => File.WriteAllText(Path, json);

        public void Dispose() => File.Delete(Path);
    }
}
