namespace Nona.Migrator.Core.Models;

public sealed record UpsertConfigEntryRequest(string Value, string? ContentType, string? Scope);
