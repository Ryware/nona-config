using Nona.FirebaseRemoteConfigMigrator.DTOs;
using Nona.FirebaseRemoteConfigMigrator.Models;
using System.Text.Json.Serialization;

namespace Nona.FirebaseRemoteConfigMigrator;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true)]
[JsonSerializable(typeof(MigrationConfiguration))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(FirebaseRemoteConfigTemplate))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(CreateProjectRequest))]
[JsonSerializable(typeof(CreateEnvironmentRequest))]
[JsonSerializable(typeof(UpsertConfigEntryRequest))]
[JsonSerializable(typeof(NonaProjectDto))]
[JsonSerializable(typeof(NonaProjectDto[]))]
[JsonSerializable(typeof(NonaEnvironmentDto))]
[JsonSerializable(typeof(NonaEnvironmentDto[]))]
internal sealed partial class SerializerContext : JsonSerializerContext;
