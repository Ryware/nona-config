using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Nona.Migrator.AwsParameterStore.Models;
using Nona.Migrator.AwsParameterStore.Services;
using NSubstitute;

namespace Nona.Migrator.AwsParameterStore.Tests;

public sealed class ParameterStoreMigrationPlannerTests
{
    [Test]
    public async Task Build_ImportsOnlyStringParameters_AsServerTextEntries()
    {
        var client = Substitute.For<IAmazonSimpleSystemsManagement>();
        GetParametersRequest? capturedRequest = null;
        client.GetParametersAsync(Arg.Any<GetParametersRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<GetParametersRequest>();
                capturedRequest = request;
                return new GetParametersResponse
                {
                    Parameters =
                    [
                        Parameter(request.Names[2], ParameterType.StringList, "list-value"),
                        Parameter(request.Names[0], ParameterType.String, "plain-value"),
                        Parameter(request.Names[1], ParameterType.SecureString, "encrypted-value")
                    ]
                };
            });
        var factory = new FakeSsmClientFactory(new Dictionary<string, IAmazonSimpleSystemsManagement>
        {
            ["eu-central-1"] = client
        });
        var mappings = new TaskDefinitionMappings(
        [
            Mapping("PLAIN", "/app/plain"),
            Mapping("SECURE", "/app/secure"),
            Mapping("LIST", "/app/list")
        ], []);

        var plan = await ParameterStoreMigrationPlanner.BuildAsync(
            mappings, "prod", "eu-central-1", factory, CancellationToken.None);

        await Assert.That(plan.Entries).Count().IsEqualTo(1);
        await Assert.That(plan.Entries[0].Key).IsEqualTo("PLAIN");
        await Assert.That(plan.Entries[0].Value).IsEqualTo("plain-value");
        await Assert.That(plan.Entries[0].Environment).IsEqualTo("prod");
        await Assert.That(plan.Entries[0].ContentType).IsEqualTo("text");
        await Assert.That(plan.Entries[0].Scope).IsEqualTo("server");
        await Assert.That(plan.Warnings).Count().IsEqualTo(2);
        await Assert.That(capturedRequest).IsNotNull();
        await Assert.That(capturedRequest!.WithDecryption).IsFalse();
    }

    [Test]
    public async Task Build_BatchesTwentyFourParametersIntoThreeRequests()
    {
        var client = Substitute.For<IAmazonSimpleSystemsManagement>();
        client.GetParametersAsync(Arg.Any<GetParametersRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<GetParametersRequest>();
                return new GetParametersResponse
                {
                    Parameters = request.Names.Select(name => Parameter(name, ParameterType.String, $"value-{name}")).ToList()
                };
            });
        var factory = new FakeSsmClientFactory(new Dictionary<string, IAmazonSimpleSystemsManagement>
        {
            ["eu-central-1"] = client
        });
        var mappings = new TaskDefinitionMappings(
            Enumerable.Range(1, 24).Select(index => Mapping($"KEY_{index}", $"/app/key-{index}")).ToArray(),
            []);

        var plan = await ParameterStoreMigrationPlanner.BuildAsync(
            mappings, "prod", "eu-central-1", factory, CancellationToken.None);

        await Assert.That(plan.Entries).Count().IsEqualTo(24);
        await client.Received(3).GetParametersAsync(
            Arg.Is<GetParametersRequest>(request => request.Names.Count <= 10),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Build_UsesArnRegionsForMixedRegionTaskDefinition()
    {
        var euClient = CreateEchoClient();
        var usClient = CreateEchoClient();
        var factory = new FakeSsmClientFactory(new Dictionary<string, IAmazonSimpleSystemsManagement>
        {
            ["eu-central-1"] = euClient,
            ["us-east-1"] = usClient
        });
        var euArn = "arn:aws:ssm:eu-central-1:111122223333:parameter/eu/key";
        var usArn = "arn:aws:ssm:us-east-1:111122223333:parameter/us/key";
        var mappings = new TaskDefinitionMappings(
        [
            new ParameterMapping("EU_KEY", euArn, "eu-central-1", euArn),
            new ParameterMapping("US_KEY", usArn, "us-east-1", usArn)
        ], []);

        var plan = await ParameterStoreMigrationPlanner.BuildAsync(
            mappings, "prod", null, factory, CancellationToken.None);

        await Assert.That(plan.Entries).Count().IsEqualTo(2);
        await euClient.Received(1).GetParametersAsync(Arg.Any<GetParametersRequest>(), Arg.Any<CancellationToken>());
        await usClient.Received(1).GetParametersAsync(Arg.Any<GetParametersRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Build_FailsWhenParameterIsMissing()
    {
        var client = Substitute.For<IAmazonSimpleSystemsManagement>();
        client.GetParametersAsync(Arg.Any<GetParametersRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetParametersResponse { InvalidParameters = ["/app/missing"] });
        var factory = new FakeSsmClientFactory(new Dictionary<string, IAmazonSimpleSystemsManagement>
        {
            ["eu-central-1"] = client
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ParameterStoreMigrationPlanner.BuildAsync(
                new TaskDefinitionMappings([Mapping("MISSING", "/app/missing")], []),
                "prod", "eu-central-1", factory, CancellationToken.None));

        await Assert.That(exception!.Message).Contains("did not return");
    }

    [Test]
    public async Task Build_RequiresRegionForBareParameterName()
    {
        var factory = new FakeSsmClientFactory(
            new Dictionary<string, IAmazonSimpleSystemsManagement>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ParameterStoreMigrationPlanner.BuildAsync(
                new TaskDefinitionMappings([Mapping("NO_REGION", "/app/key")], []),
                "prod", null, factory, CancellationToken.None));

        await Assert.That(exception!.Message).Contains("AWS region is required");
    }

    private static IAmazonSimpleSystemsManagement CreateEchoClient()
    {
        var client = Substitute.For<IAmazonSimpleSystemsManagement>();
        client.GetParametersAsync(Arg.Any<GetParametersRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<GetParametersRequest>();
                return new GetParametersResponse
                {
                    Parameters = request.Names.Select(name => new Parameter
                    {
                        Name = name.StartsWith("arn:", StringComparison.Ordinal)
                            ? "/" + name.Split(':', 6)[5]["parameter/".Length..]
                            : name,
                        Type = ParameterType.String,
                        Value = "value"
                    }).ToList()
                };
            });
        return client;
    }

    private static ParameterMapping Mapping(string key, string name)
        => new(key, name, null, name);

    private static Parameter Parameter(string name, ParameterType type, string value)
        => new() { Name = name, ARN = name.StartsWith("arn:", StringComparison.Ordinal) ? name : null, Type = type, Value = value };

    private sealed class FakeSsmClientFactory(
        IReadOnlyDictionary<string, IAmazonSimpleSystemsManagement> clients) : ISsmClientFactory
    {
        public string? ResolveDefaultRegion() => null;

        public IAmazonSimpleSystemsManagement Create(string region)
            => clients.TryGetValue(region, out var client)
                ? client
                : throw new InvalidOperationException($"Unexpected region '{region}'.");
    }
}
