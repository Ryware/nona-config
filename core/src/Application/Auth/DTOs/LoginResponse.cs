namespace Nona.Application.Auth.DTOs;

public record LoginResponse(string Token, string Username, string Role, DateTime ExpiresAt);
