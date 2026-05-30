namespace Nona.Cli;

internal sealed class ConfigEntryDto
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public DateTime UpdatedAt { get; init; }
}
