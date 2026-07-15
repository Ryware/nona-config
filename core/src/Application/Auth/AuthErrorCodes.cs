namespace Nona.Application.Auth;

public static class AuthErrorCodes
{
    public const string InvitationInvalidOrUsed = "invitation_invalid_or_used";
    public const string InvitationSsoEmailMismatch = "invitation_sso_email_mismatch";
    public const string RegistrationDisabled = "registration_disabled";
    public const string UserAlreadyExists = "user_already_exists";
    public const string SsoUserNotRegistered = "sso_user_not_registered";
}
