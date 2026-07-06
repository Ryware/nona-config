# Nona.OpenFeature.Provider

OpenFeature provider for **Nona**. Use Nona remote config and feature flags through the OpenFeature .NET SDK.

## Install

```bash
dotnet add package Nona.Client
dotnet add package Nona.OpenFeature.Provider
```

## Usage

```csharp
using Nona.Client;
using Nona.OpenFeature.Provider;
using OpenFeature;

using var nona = new NonaClient(new NonaClientOptions
{
    BaseAddress = new Uri("https://nona.example.com"),
    ApiKey = "your-api-key"
});

await Api.Instance.SetProviderAsync(
    new NonaOpenFeatureProvider(nona, "production"));

var client = Api.Instance.GetClient();
var enabled = await client.GetBooleanValueAsync("Features:Checkout", false);
```

Nona API keys are bound to a project, so provider configuration only needs the Nona client and environment id.

## Integration test

The test suite includes an optional real-server test. Seed a Nona environment with these entries, or override the key/value environment variables:

- `openfeature:boolean` = `true` (`boolean`)
- `openfeature:number` = `42` (`number`)
- `openfeature:string` = `Checkout` (`text`)
- `openfeature:object` = `{"color":"green","enabled":true}` (`json`)

```bash
NONA_INTEGRATION_BASE_URL=http://localhost:18080 \
NONA_INTEGRATION_API_KEY=your-api-key \
NONA_INTEGRATION_ENVIRONMENT_ID=production \
dotnet test tests/Nona.OpenFeature.Provider.Tests/Nona.OpenFeature.Provider.Tests.csproj
```
