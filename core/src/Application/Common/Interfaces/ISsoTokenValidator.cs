namespace Nona.Application.Common.Interfaces;

public interface ISsoTokenValidator
{
    string Provider { get; }
    bool IsEnabled { get; }
    Task<SsoTokenValidationResult> ValidateAsync(string idToken, CancellationToken ct = default);
}

public sealed record SsoIdentity(
    string Provider,
    string Subject,
    string Issuer,
    string? Email,
    string? Name,
    string? TenantId,
    bool EmailVerified);

public sealed record SsoTokenValidationResult(bool Success, SsoIdentity? Identity, string FailureCategory)
{
    public static SsoTokenValidationResult Succeeded(SsoIdentity identity) => new(true, identity, string.Empty);
    public static SsoTokenValidationResult Failed(string failureCategory) => new(false, null, failureCategory);
}
