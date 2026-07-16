using Nona.Domain.Enums;

namespace Nona.Domain.Entities;

public class ConfigReleaseEntry
{
    public required string Project { get; init; }
    public required string Environment { get; init; }
    public required string ReleaseVersion { get; init; }
    public required string Key { get; init; }
    public required string Value { get; init; }
    public string ContentType { get; init; } = "text";
    public KeyScope Scope { get; init; } = KeyScope.All;
}
