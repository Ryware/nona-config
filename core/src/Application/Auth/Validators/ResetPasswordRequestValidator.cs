using Nona.Application.Auth.DTOs;

namespace Nona.Application.Auth.Validators;

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword).PasswordPolicy();
    }
}
