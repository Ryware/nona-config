namespace Nona.Migrator.Core.Models;

public sealed record PlannedConfigEntry(
    string Environment,
    string Key,
    string Value,
    string ContentType,
    string Scope,
    string SourceLabel);
