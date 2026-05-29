using System.Text.Json.Serialization;
using Nona.Migrator.Core.Models;
using Nona.Migrator.Core.DTO;

namespace Nona.Migrator.Core;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true)]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(CreateProjectRequest))]
[JsonSerializable(typeof(CreateEnvironmentRequest))]
[JsonSerializable(typeof(UpsertConfigEntryRequest))]
[JsonSerializable(typeof(RerollApiKeysRequest))]
[JsonSerializable(typeof(CreateUserRequest))]
[JsonSerializable(typeof(CreateUserResponse))]
[JsonSerializable(typeof(NonaProjectDto))]
[JsonSerializable(typeof(NonaProjectDto[]))]
[JsonSerializable(typeof(NonaEnvironmentDto))]
[JsonSerializable(typeof(NonaEnvironmentDto[]))]
public sealed partial class NonaSerializerContext : JsonSerializerContext;
