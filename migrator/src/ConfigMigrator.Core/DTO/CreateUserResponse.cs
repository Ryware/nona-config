namespace Nona.Migrator.Core.DTO;

public sealed class CreateUserResponse
{
    public CreatedUserDto User { get; init; } = new();
    public string InvitationToken { get; init; } = string.Empty;
}

public sealed class CreatedUserDto
{
    public long Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
}
