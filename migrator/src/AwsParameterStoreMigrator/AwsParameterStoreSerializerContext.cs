using System.Text.Json.Serialization;
using Nona.Migrator.AwsParameterStore.Models;

namespace Nona.Migrator.AwsParameterStore;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(EcsTaskDefinitionDocument))]
internal sealed partial class AwsParameterStoreSerializerContext : JsonSerializerContext;
