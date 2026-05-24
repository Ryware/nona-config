using Nona.Migrator.FirebaseRemoteConfig.Service;
using System.Text.Json.Serialization;

namespace Nona.FirebaseRemoteConfigMigrator;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true)]
[JsonSerializable(typeof(FirebaseRemoteConfigTemplate))]
[JsonSerializable(typeof(MigrationConfiguration))]
internal sealed partial class FirebaseSerializerContext : JsonSerializerContext;
