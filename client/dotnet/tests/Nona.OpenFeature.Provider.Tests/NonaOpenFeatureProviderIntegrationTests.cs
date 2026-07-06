using Nona.Client;
using Nona.OpenFeature.Provider;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeatureValue = global::OpenFeature.Model.Value;

namespace Nona.OpenFeature.Provider.Tests;

public sealed class NonaOpenFeatureProviderIntegrationTests
{
    [NonaIntegrationFact]
    public async Task OpenFeatureProvider_ResolvesValuesFromRealNonaConfigServer()
    {
        var settings = NonaIntegrationSettings.Create();
        var domain = $"nona-dotnet-integration-{Guid.NewGuid():N}";

        using var nona = new NonaClient(settings.BaseUrl, settings.ApiKey);
        await Api.Instance.SetProviderAsync(
            domain,
            new NonaOpenFeatureProvider(nona, settings.EnvironmentId));

        var client = Api.Instance.GetClient(domain);

        Assert.Equal(
            settings.BooleanValue,
            await client.GetBooleanValueAsync(settings.BooleanKey, !settings.BooleanValue));
        Assert.Equal(
            settings.NumberValue,
            await client.GetDoubleValueAsync(settings.NumberKey, settings.NumberValue + 1));
        Assert.Equal(
            settings.StringValue,
            await client.GetStringValueAsync(settings.StringKey, "fallback"));

        var objectValue = await client.GetObjectValueAsync(settings.ObjectKey, new OpenFeatureValue());
        Assert.True(objectValue.IsStructure);
        var structure = objectValue.AsStructure ?? throw new InvalidOperationException("Expected structure value.");
        Assert.Equal("green", structure.GetValue("color").AsString);
        Assert.True(structure.GetValue("enabled").AsBoolean);

        var missing = await client.GetBooleanDetailsAsync($"missing-{Guid.NewGuid():N}", true);
        Assert.True(missing.Value);
        Assert.Equal(ErrorType.FlagNotFound, missing.ErrorType);
    }
}

public sealed class NonaIntegrationFactAttribute : FactAttribute
{
    public NonaIntegrationFactAttribute()
    {
        if (!NonaIntegrationSettings.IsConfigured)
        {
            Skip = "Set NONA_INTEGRATION_BASE_URL, NONA_INTEGRATION_API_KEY, and NONA_INTEGRATION_ENVIRONMENT_ID to run.";
        }
    }
}

internal sealed class NonaIntegrationSettings
{
    private NonaIntegrationSettings(
        string baseUrl,
        string apiKey,
        string environmentId,
        string booleanKey,
        bool booleanValue,
        string numberKey,
        double numberValue,
        string stringKey,
        string stringValue,
        string objectKey)
    {
        BaseUrl = baseUrl;
        ApiKey = apiKey;
        EnvironmentId = environmentId;
        BooleanKey = booleanKey;
        BooleanValue = booleanValue;
        NumberKey = numberKey;
        NumberValue = numberValue;
        StringKey = stringKey;
        StringValue = stringValue;
        ObjectKey = objectKey;
    }

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NONA_INTEGRATION_BASE_URL")) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NONA_INTEGRATION_API_KEY")) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NONA_INTEGRATION_ENVIRONMENT_ID"));

    public string BaseUrl { get; }

    public string ApiKey { get; }

    public string EnvironmentId { get; }

    public string BooleanKey { get; }

    public bool BooleanValue { get; }

    public string NumberKey { get; }

    public double NumberValue { get; }

    public string StringKey { get; }

    public string StringValue { get; }

    public string ObjectKey { get; }

    public static NonaIntegrationSettings Create()
    {
        return new NonaIntegrationSettings(
            Required("NONA_INTEGRATION_BASE_URL"),
            Required("NONA_INTEGRATION_API_KEY"),
            Required("NONA_INTEGRATION_ENVIRONMENT_ID"),
            Environment.GetEnvironmentVariable("NONA_INTEGRATION_BOOLEAN_KEY") ?? "openfeature:boolean",
            bool.Parse(Environment.GetEnvironmentVariable("NONA_INTEGRATION_BOOLEAN_VALUE") ?? "true"),
            Environment.GetEnvironmentVariable("NONA_INTEGRATION_NUMBER_KEY") ?? "openfeature:number",
            double.Parse(Environment.GetEnvironmentVariable("NONA_INTEGRATION_NUMBER_VALUE") ?? "42", System.Globalization.CultureInfo.InvariantCulture),
            Environment.GetEnvironmentVariable("NONA_INTEGRATION_STRING_KEY") ?? "openfeature:string",
            Environment.GetEnvironmentVariable("NONA_INTEGRATION_STRING_VALUE") ?? "Checkout",
            Environment.GetEnvironmentVariable("NONA_INTEGRATION_OBJECT_KEY") ?? "openfeature:object");
    }

    private static string Required(string name)
    {
        return Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"{name} is required.");
    }
}
