namespace Nona.Application.Auth.DTOs;

public record SsoLoginRequest(string IdToken);

public record SsoRedirectCredentialResponse(string IdToken);
