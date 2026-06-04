namespace Nona.Cli.Keys;

internal sealed class ApiKeyDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string? Environment { get; set; }
    public string Scope { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

internal sealed class CreateApiKeyRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Environment { get; set; }
    public string? Scope { get; set; }
}
