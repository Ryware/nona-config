using Nona.Migrator.AwsParameterStore.Models;
using Nona.Migrator.AwsParameterStore.Services;

namespace Nona.Migrator.AwsParameterStore.Tests;

public sealed class EcsTaskDefinitionParserTests
{
    [Test]
    public async Task Load_AcceptsRawTaskDefinition()
    {
        var result = await LoadJsonAsync("""
            {
              "containerDefinitions": [
                {
                  "name": "api",
                  "secrets": [
                    { "name": "RAW_KEY", "valueFrom": "/app/raw" }
                  ]
                }
              ]
            }
            """);

        await Assert.That(result.Parameters).Count().IsEqualTo(1);
        await Assert.That(result.Parameters[0].Key).IsEqualTo("RAW_KEY");
    }

    [Test]
    public async Task Load_AcceptsDescribeTaskDefinitionEnvelope()
    {
        var result = await LoadJsonAsync("""
            {
              "taskDefinition": {
                "containerDefinitions": [
                  {
                    "name": "api",
                    "secrets": [
                      { "name": "WRAPPED_KEY", "valueFrom": "/app/wrapped" }
                    ]
                  }
                ]
              }
            }
            """);

        await Assert.That(result.Parameters).Count().IsEqualTo(1);
        await Assert.That(result.Parameters[0].Key).IsEqualTo("WRAPPED_KEY");
    }

    [Test]
    public async Task Load_RejectsDocumentWithoutTaskDefinitionShape()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => LoadJsonAsync("{}"));

        await Assert.That(exception!.Message).Contains("containerDefinitions");
    }

    [Test]
    public async Task Load_RejectsAmbiguousTaskDefinitionShapes()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => LoadJsonAsync("""
            {
              "containerDefinitions": [],
              "taskDefinition": {
                "containerDefinitions": []
              }
            }
            """));

        await Assert.That(exception!.Message).Contains("both");
    }

    [Test]
    public async Task Parse_MapsSsmSecretsAcrossContainers_AndSkipsSecretsManager()
    {
        var taskDefinition = new EcsTaskDefinition
        {
            ContainerDefinitions =
            [
                new EcsContainerDefinition
                {
                    Name = "api",
                    Secrets =
                    [
                        new EcsSecret
                        {
                            Name = "TRANSFER_TOKEN_SALT",
                            ValueFrom = "arn:aws:ssm:eu-central-1:111122223333:parameter/app/transfer_token_salt"
                        },
                        new EcsSecret
                        {
                            Name = "IGNORED_SECRET",
                            ValueFrom = "arn:aws:secretsmanager:eu-central-1:111122223333:secret:ignored"
                        }
                    ]
                },
                new EcsContainerDefinition
                {
                    Name = "worker",
                    Secrets = [new EcsSecret { Name = "QUEUE_NAME", ValueFrom = "/app/queue" }]
                }
            ]
        };

        var result = EcsTaskDefinitionParser.Parse(taskDefinition);

        await Assert.That(result.Parameters).Count().IsEqualTo(2);
        await Assert.That(result.Parameters.Single(mapping => mapping.Key == "TRANSFER_TOKEN_SALT").Region)
            .IsEqualTo("eu-central-1");
        await Assert.That(result.Parameters.Single(mapping => mapping.Key == "QUEUE_NAME").Region).IsNull();
        await Assert.That(result.Warnings).Count().IsEqualTo(1);
        await Assert.That(result.Warnings[0]).Contains("Secrets Manager");
    }

    [Test]
    public async Task Parse_DeduplicatesSameKeyAndReferenceAcrossContainers()
    {
        const string reference = "arn:aws:ssm:eu-west-1:111122223333:parameter/shared/key";
        var taskDefinition = new EcsTaskDefinition
        {
            ContainerDefinitions =
            [
                new EcsContainerDefinition { Name = "one", Secrets = [new EcsSecret { Name = "SHARED_KEY", ValueFrom = reference }] },
                new EcsContainerDefinition { Name = "two", Secrets = [new EcsSecret { Name = "SHARED_KEY", ValueFrom = reference }] }
            ]
        };

        var result = EcsTaskDefinitionParser.Parse(taskDefinition);

        await Assert.That(result.Parameters).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Parse_FailsWhenSameKeyMapsToDifferentParameters()
    {
        var taskDefinition = new EcsTaskDefinition
        {
            ContainerDefinitions =
            [
                new EcsContainerDefinition
                {
                    Name = "one",
                    Secrets = [new EcsSecret { Name = "SHARED_KEY", ValueFrom = "/one" }]
                },
                new EcsContainerDefinition
                {
                    Name = "two",
                    Secrets = [new EcsSecret { Name = "SHARED_KEY", ValueFrom = "/two" }]
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => EcsTaskDefinitionParser.Parse(taskDefinition));

        await Assert.That(exception!.Message).Contains("maps to multiple");
    }

    [Test]
    public async Task Parse_FailsForMalformedSsmArn()
    {
        var taskDefinition = new EcsTaskDefinition
        {
            ContainerDefinitions =
            [
                new EcsContainerDefinition
                {
                    Secrets = [new EcsSecret { Name = "VALID_KEY", ValueFrom = "arn:aws:ssm::111122223333:parameter/app/key" }]
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => EcsTaskDefinitionParser.Parse(taskDefinition));

        await Assert.That(exception!.Message).Contains("malformed SSM parameter ARN");
    }

    private static async Task<TaskDefinitionMappings> LoadJsonAsync(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nona-ecs-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, json);
            return await EcsTaskDefinitionParser.LoadAsync(path, CancellationToken.None);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
