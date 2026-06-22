using Nona.FirebaseRemoteConfigMigrator.Models;
using Nona.FirebaseRemoteConfigMigrator.Options;
using Nona.Migrator.Core.Models;
using Nona.Migrator.Core.Services;
using Nona.Migrator.FirebaseRemoteConfig.Service;

namespace Nona.FirebaseRemoteConfigMigrator.Tests;

public class MigrationPlannerTests
{
    [Test]
    public async Task FirebaseOptions_UsesStaticClientAndServerSources()
    {
        var sources = new FirebaseOptions().GetImportSources();

        await Assert.That(sources).Count().IsEqualTo(2);
        await Assert.That(sources[0].Namespace).IsEqualTo("firebase");
        await Assert.That(sources[0].Scope).IsEqualTo("client");
        await Assert.That(sources[1].Namespace).IsEqualTo("firebase-server");
        await Assert.That(sources[1].Scope).IsEqualTo("server");
    }

    [Test]
    public async Task FirebaseOptions_UsesConfiguredSources_WhenProvided()
    {
        var sources = new FirebaseOptions
        {
            Sources =
            [
                new FirebaseImportSource { Namespace = "custom-client", Scope = "client" },
                new FirebaseImportSource { Namespace = "custom-server", Scope = "server" }
            ]
        }.GetImportSources();

        await Assert.That(sources).Count().IsEqualTo(2);
        await Assert.That(sources[0].Namespace).IsEqualTo("custom-client");
        await Assert.That(sources[1].Namespace).IsEqualTo("custom-server");
    }

    [Test]
    public async Task FirebaseOptions_UsesLegacyNamespace_WhenSourcesAreNotProvided()
    {
        var sources = new FirebaseOptions
        {
            Namespace = "legacy-namespace"
        }.GetImportSources();

        await Assert.That(sources).Count().IsEqualTo(1);
        await Assert.That(sources[0].Namespace).IsEqualTo("legacy-namespace");
        await Assert.That(sources[0].Scope).IsEqualTo("all");
    }

    [Test]
    public async Task Build_AppliesDefaultValueToMappedAndDefaultEnvironments()
    {
        var template = new FirebaseRemoteConfigTemplate
        {
            Conditions =
            [
                new FirebaseCondition { Name = "production" }
            ],
            Parameters = new Dictionary<string, FirebaseParameter>
            {
                ["api_url"] = new()
                {
                    ValueType = "STRING",
                    DefaultValue = new FirebaseValue { Value = "https://dev.example.com" },
                    ConditionalValues = new Dictionary<string, FirebaseValue>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["production"] = new() { Value = "https://prod.example.com" }
                    }
                }
            }
        };

        var options = new MigrationOptions
        {
            DefaultValueEnvironments = ["dev"],
            ConditionEnvironmentMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["production"] = "prod"
            }
        };

        var plan = MigrationPlanner.Build(template, options, "client");

        await Assert.That(plan.Environments).Count().IsEqualTo(2);
        await Assert.That(plan.Entries).Count().IsEqualTo(2);
        await Assert.That(plan.Entries.Single(entry => entry.Environment == "dev").Value).IsEqualTo("https://dev.example.com");
        await Assert.That(plan.Entries.Single(entry => entry.Environment == "prod").Value).IsEqualTo("https://prod.example.com");
        await Assert.That(plan.Entries.Select(static entry => entry.ContentType).Distinct()).IsEquivalentTo(["text"]);
        await Assert.That(plan.Entries.Select(static entry => entry.Scope).Distinct()).IsEquivalentTo(["client"]);
    }

    [Test]
    public async Task Build_UsesFirebaseConditionOrder_WhenManyConditionsMapToSameEnvironment()
    {
        var template = new FirebaseRemoteConfigTemplate
        {
            Conditions =
            [
                new FirebaseCondition { Name = "prod-hotfix" },
                new FirebaseCondition { Name = "production" }
            ],
            Parameters = new Dictionary<string, FirebaseParameter>
            {
                ["feature_flag"] = new()
                {
                    ValueType = "BOOLEAN",
                    DefaultValue = new FirebaseValue { Value = "false" },
                    ConditionalValues = new Dictionary<string, FirebaseValue>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["production"] = new() { Value = "true" },
                        ["prod-hotfix"] = new() { Value = "force-on" }
                    }
                }
            }
        };

        var options = new MigrationOptions
        {
            ConditionEnvironmentMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["production"] = "prod",
                ["prod-hotfix"] = "prod"
            }
        };

        var plan = MigrationPlanner.Build(template, options, "server");

        await Assert.That(plan.Entries).Count().IsEqualTo(1);
        await Assert.That(plan.Entries[0].Environment).IsEqualTo("prod");
        await Assert.That(plan.Entries[0].Value).IsEqualTo("force-on");
        await Assert.That(plan.Entries[0].ContentType).IsEqualTo("boolean");
        await Assert.That(plan.Entries[0].Scope).IsEqualTo("server");
        await Assert.That(plan.Entries[0].SourceLabel).IsEqualTo("condition:prod-hotfix");
    }

    [Test]
    public async Task Build_AllowsMissingFirebaseCollections()
    {
        var template = new FirebaseRemoteConfigTemplate
        {
            Conditions = null,
            Parameters = new Dictionary<string, FirebaseParameter>
            {
                ["plain_key"] = new()
                {
                    ValueType = "NUMBER",
                    DefaultValue = new FirebaseValue { Value = "abc" },
                    ConditionalValues = null
                }
            },
            ParameterGroups = null
        };

        var options = new MigrationOptions
        {
            DefaultValueEnvironments = ["dev"]
        };

        var plan = MigrationPlanner.Build(template, options, "client");

        await Assert.That(plan.Entries).Count().IsEqualTo(1);
        await Assert.That(plan.Entries[0].Environment).IsEqualTo("dev");
        await Assert.That(plan.Entries[0].Value).IsEqualTo("abc");
        await Assert.That(plan.Entries[0].ContentType).IsEqualTo("number");
    }

    [Test]
    public async Task Merge_CombinesClientAndServerIntoAll_WhenValueMatches()
    {
        var merged = MigrationPlanMerger.Merge(
        [
            new MigrationPlan(
                ["prod"],
                [new PlannedConfigEntry("prod", "shared_key", "42", "number", "client", "Client/defaultValue")],
                [],
                1),
            new MigrationPlan(
                ["prod"],
                [new PlannedConfigEntry("prod", "shared_key", "42", "number", "server", "Server/defaultValue")],
                [],
                1)
        ]);

        await Assert.That(merged.Entries).Count().IsEqualTo(1);
        await Assert.That(merged.Entries[0].Scope).IsEqualTo("all");
        await Assert.That(merged.Entries[0].ContentType).IsEqualTo("number");
    }

    [Test]
    public async Task Merge_KeepsFirstAndWarns_WhenSameKeyHasDifferentValuesAcrossScopes()
    {
        var merged = MigrationPlanMerger.Merge(
        [
            new MigrationPlan(
                ["prod"],
                [new PlannedConfigEntry("prod", "shared_key", "42", "text", "client", "Client/defaultValue")],
                [],
                1),
            new MigrationPlan(
                ["prod"],
                [new PlannedConfigEntry("prod", "shared_key", "77", "text", "server", "Server/defaultValue")],
                [],
                1)
        ]);

        await Assert.That(merged.Entries).Count().IsEqualTo(1);
        await Assert.That(merged.Entries[0].Value).IsEqualTo("42");
        await Assert.That(merged.Warnings).Count().IsEqualTo(1);
        await Assert.That(merged.Warnings[0]).Contains("Keeping first value");
    }

    [Test]
    public async Task Merge_KeepsFirstTypeAndWarns_WhenSameKeyValueHasDifferentTypesAcrossScopes()
    {
        var merged = MigrationPlanMerger.Merge(
        [
            new MigrationPlan(
                ["prod"],
                [new PlannedConfigEntry("prod", "shared_key", "42", "number", "client", "Client/defaultValue")],
                [],
                1),
            new MigrationPlan(
                ["prod"],
                [new PlannedConfigEntry("prod", "shared_key", "42", "text", "server", "Server/defaultValue")],
                [],
                1)
        ]);

        await Assert.That(merged.Entries).Count().IsEqualTo(1);
        await Assert.That(merged.Entries[0].ContentType).IsEqualTo("number");
        await Assert.That(merged.Warnings).Count().IsEqualTo(1);
        await Assert.That(merged.Warnings[0]).Contains("Keeping first type");
    }

    [Test]
    public async Task Merge_RenamesConflictingKeys_WhenOptionEnabled()
    {
        var merged = MigrationPlanMerger.Merge(
        [
            new MigrationPlan(
                ["prod"],
                [new PlannedConfigEntry("prod", "shared_key", "42", "text", "client", "Client/defaultValue")],
                [],
                1),
            new MigrationPlan(
                ["prod"],
                [new PlannedConfigEntry("prod", "shared_key", "77", "text", "server", "Server/defaultValue")],
                [],
                1)
        ],
        renameConflictingKeys: true);

        await Assert.That(merged.Entries).Count().IsEqualTo(2);
        await Assert.That(merged.Entries.Select(static entry => entry.Key).OrderBy(static key => key).ToArray())
            .IsEquivalentTo(["shared_key", "shared_key_1"]);
        await Assert.That(merged.Warnings).Count().IsEqualTo(1);
        await Assert.That(merged.Warnings[0]).Contains("will be renamed to 'shared_key_1'");
    }

    [Test]
    public async Task Merge_ReusesSameRenamedKeyAcrossEnvironments_WhenOptionEnabled()
    {
        var merged = MigrationPlanMerger.Merge(
        [
            new MigrationPlan(
                ["dev", "prod"],
                [
                    new PlannedConfigEntry("dev", "shared_key", "42", "text", "client", "Client/defaultValue"),
                    new PlannedConfigEntry("prod", "shared_key", "42", "text", "client", "Client/defaultValue")
                ],
                [],
                1),
            new MigrationPlan(
                ["dev", "prod"],
                [
                    new PlannedConfigEntry("dev", "shared_key", "77", "text", "server", "Server/defaultValue"),
                    new PlannedConfigEntry("prod", "shared_key", "88", "text", "server", "Server/defaultValue")
                ],
                [],
                1)
        ],
        renameConflictingKeys: true);

        await Assert.That(merged.Entries).Count().IsEqualTo(4);
        await Assert.That(merged.Entries.Where(static entry => entry.Scope == "server").Select(static entry => entry.Key).Distinct().ToArray())
            .IsEquivalentTo(["shared_key_1"]);
        await Assert.That(merged.Warnings).Count().IsEqualTo(1);
    }
}
