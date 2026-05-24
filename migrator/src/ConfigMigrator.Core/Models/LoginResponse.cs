namespace Nona.Migrator.Core.Models;

public sealed record LoginResponse(string Token, string Username, string Role, DateTime ExpiresAt);
