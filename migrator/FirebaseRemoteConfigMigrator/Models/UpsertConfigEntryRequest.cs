namespace Nona.FirebaseRemoteConfigMigrator.Models;

internal sealed record UpsertConfigEntryRequest(string Value, string? ContentType, string? Scope);
