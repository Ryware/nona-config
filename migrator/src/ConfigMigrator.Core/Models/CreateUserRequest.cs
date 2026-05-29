namespace Nona.Migrator.Core.Models;

public sealed record CreateUserRequest(string Name, string Email, string? Role, string? Scope);
