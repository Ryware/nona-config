using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nona.Client;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Nona.OpenFeature.Provider;

public sealed class NonaOpenFeatureProvider : FeatureProvider
{
    private const string DefaultProviderName = "nona";
    private static readonly ImmutableMetadata EmptyMetadata = new ImmutableMetadata();

    private readonly NonaClient _client;
    private readonly string _environmentId;
    private readonly Metadata _metadata;

    public NonaOpenFeatureProvider(
        NonaClient client,
        string environmentId,
        string providerName = DefaultProviderName)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        if (string.IsNullOrWhiteSpace(environmentId))
        {
            throw new ArgumentException("Environment id is required.", nameof(environmentId));
        }

        _environmentId = environmentId;
        _metadata = new Metadata(string.IsNullOrWhiteSpace(providerName) ? DefaultProviderName : providerName);
    }

    public override Metadata GetMetadata()
    {
        return _metadata;
    }

    public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(
        string flagKey,
        bool defaultValue,
        EvaluationContext? context,
        CancellationToken cancellationToken = default)
    {
        return ResolveFlagAsync(flagKey, defaultValue, config =>
        {
            if (bool.TryParse(config.Value.Trim(), out var value))
            {
                return Success(flagKey, value, config);
            }

            return Error(
                flagKey,
                defaultValue,
                ErrorType.TypeMismatch,
                $"Nona flag '{flagKey}' cannot be evaluated as a boolean.");
        }, cancellationToken);
    }

    public override Task<ResolutionDetails<string>> ResolveStringValueAsync(
        string flagKey,
        string defaultValue,
        EvaluationContext? context,
        CancellationToken cancellationToken = default)
    {
        return ResolveFlagAsync(
            flagKey,
            defaultValue,
            config => Success(flagKey, config.Value, config),
            cancellationToken);
    }

    public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(
        string flagKey,
        int defaultValue,
        EvaluationContext? context,
        CancellationToken cancellationToken = default)
    {
        return ResolveFlagAsync(flagKey, defaultValue, config =>
        {
            if (int.TryParse(config.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return Success(flagKey, value, config);
            }

            return Error(
                flagKey,
                defaultValue,
                ErrorType.TypeMismatch,
                $"Nona flag '{flagKey}' cannot be evaluated as an integer.");
        }, cancellationToken);
    }

    public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(
        string flagKey,
        double defaultValue,
        EvaluationContext? context,
        CancellationToken cancellationToken = default)
    {
        return ResolveFlagAsync(flagKey, defaultValue, config =>
        {
            if (double.TryParse(
                config.Value.Trim(),
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var value) &&
                !double.IsNaN(value) &&
                !double.IsInfinity(value))
            {
                return Success(flagKey, value, config);
            }

            return Error(
                flagKey,
                defaultValue,
                ErrorType.TypeMismatch,
                $"Nona flag '{flagKey}' cannot be evaluated as a double.");
        }, cancellationToken);
    }

    public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(
        string flagKey,
        Value defaultValue,
        EvaluationContext? context,
        CancellationToken cancellationToken = default)
    {
        return ResolveFlagAsync(flagKey, defaultValue, config =>
        {
            try
            {
                using var document = JsonDocument.Parse(config.Value);
                return Success(flagKey, ConvertJsonElement(document.RootElement), config);
            }
            catch (JsonException)
            {
                return Error(
                    flagKey,
                    defaultValue,
                    ErrorType.ParseError,
                    $"Nona flag '{flagKey}' cannot be parsed as JSON.");
            }
        }, cancellationToken);
    }

    private async Task<ResolutionDetails<T>> ResolveFlagAsync<T>(
        string flagKey,
        T defaultValue,
        Func<NonaConfigValue, ResolutionDetails<T>> resolve,
        CancellationToken cancellationToken)
    {
        try
        {
            var config = await _client.GetConfigValueAsync(_environmentId, flagKey, cancellationToken)
                .ConfigureAwait(false);
            return resolve(config);
        }
        catch (NonaClientException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return Error(flagKey, defaultValue, ErrorType.FlagNotFound, ex.Message);
        }
        catch (Exception ex)
        {
            return Error(flagKey, defaultValue, ErrorType.General, ex.Message);
        }
    }

    private static ResolutionDetails<T> Success<T>(
        string flagKey,
        T value,
        NonaConfigValue config)
    {
        return new ResolutionDetails<T>(
            flagKey,
            value,
            ErrorType.None,
            null,
            Reason.Static,
            null,
            new ImmutableMetadata(new Dictionary<string, object>
            {
                ["contentType"] = config.ContentType,
                ["nonaKey"] = flagKey
            }));
    }

    private static ResolutionDetails<T> Error<T>(
        string flagKey,
        T defaultValue,
        ErrorType errorType,
        string errorMessage)
    {
        return new ResolutionDetails<T>(
            flagKey,
            defaultValue,
            errorType,
            errorMessage,
            Reason.Error,
            null,
            EmptyMetadata);
    }

    private static Value ConvertJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var builder = Structure.Builder();
                foreach (var property in element.EnumerateObject())
                {
                    builder.Set(property.Name, ConvertJsonElement(property.Value));
                }

                return new Value(builder.Build());

            case JsonValueKind.Array:
                var values = new List<Value>();
                foreach (var item in element.EnumerateArray())
                {
                    values.Add(ConvertJsonElement(item));
                }

                return new Value(values);

            case JsonValueKind.String:
                return new Value(element.GetString() ?? string.Empty);

            case JsonValueKind.Number:
                return element.TryGetInt32(out var integer)
                    ? new Value(integer)
                    : new Value(element.GetDouble());

            case JsonValueKind.True:
            case JsonValueKind.False:
                return new Value(element.GetBoolean());

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                return new Value();
        }
    }
}
