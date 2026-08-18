using System.Text.Json.Serialization;
using Nona.Migrator.AwsParameterStore.Models;

namespace Nona.Migrator.AwsParameterStore;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(EcsTaskDefinition))]
internal sealed partial class AwsParameterStoreSerializerContext : JsonSerializerContext;
