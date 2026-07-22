using System.Text.Json.Serialization.Metadata;
using Nona.Client;

namespace Nona.Client.Tests;

internal static class NonaClientSourceCompatibilityFixture
{
    public static void LegacyDefaultCallShapesCompile<T>(
        NonaClient client,
        JsonTypeInfo<T> jsonTypeInfo)
    {
        _ = client.GetConfigValueAsync("flag", default);
        _ = client.TryGetConfigValueAsync("flag", default);
        _ = client.GetStringValueAsync("flag", default);
        _ = client.GetJsonValueAsync("flag", jsonTypeInfo, default);
    }
}
