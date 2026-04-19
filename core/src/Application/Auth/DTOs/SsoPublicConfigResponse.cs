namespace Nona.Application.Auth.DTOs;

public record SsoPublicConfigResponse(
    SsoProviderPublicConfig Google,
    SsoProviderPublicConfig Microsoft);

public record SsoProviderPublicConfig(
    bool Enabled,
    string? ClientId,
    string? Authority = null,
    string? TenantId = null);
