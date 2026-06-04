using Nona.Application.Auth.DTOs;

namespace Nona.Application.Auth.Validators;

public class CompleteInvitationPasswordRequestValidator : AbstractValidator<CompleteInvitationPasswordRequest>
{
    public CompleteInvitationPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword)
            .NotEmpty();
    }
}
