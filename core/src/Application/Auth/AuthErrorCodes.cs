namespace Nona.Application.Auth;

public static class AuthErrorCodes
{
    public const string InvitationInvalidOrUsed = "invitation_invalid_or_used";
    public const string InvitationSsoEmailMismatch = "invitation_sso_email_mismatch";
    public const string RegistrationDisabled = "registration_disabled";
    public const string UserAlreadyExists = "user_already_exists";
    public const string SsoUserNotRegistered = "sso_user_not_registered";
    public const string PasswordResetInvalidOrUsed = "password_reset_invalid_or_used";
    public const string PasswordResetUnavailable = "password_reset_unavailable";
    public const string PasswordResetSelfNotAllowed = "password_reset_self_not_allowed";
    public const string PasswordChangeUnavailable = "password_change_unavailable";
    public const string CurrentPasswordInvalid = "current_password_invalid";
    public const string NewPasswordMustDiffer = "new_password_must_differ";
}
