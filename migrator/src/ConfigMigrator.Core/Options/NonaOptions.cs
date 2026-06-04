namespace Nona.Migrator.Core.Options;

public sealed record NonaOptions : IEquatable<NonaOptions>
{
    public string BaseUrl { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? Password { get; init; }
    public string? BearerToken { get; init; }
}
