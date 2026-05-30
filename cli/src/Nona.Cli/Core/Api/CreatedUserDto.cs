namespace Nona.Cli;

internal sealed class CreatedUserDto
{
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string InvitationToken { get; init; } = string.Empty;
}
