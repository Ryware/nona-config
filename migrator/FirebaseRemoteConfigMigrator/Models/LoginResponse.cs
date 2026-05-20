namespace Nona.FirebaseRemoteConfigMigrator.Models;

internal sealed record LoginResponse(string Token, string Username, string Role, DateTime ExpiresAt);
