using Nona.Application.Admin.Environments.Commands;

namespace Nona.Application.Admin.Environments.Validators;

public class CreateEnvironmentRequestValidator : AbstractValidator<CreateEnvironmentRequest>
{
    public CreateEnvironmentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .Matches(@"^[a-zA-Z0-9-]+$")
            .WithMessage("Name must be alphanumeric with hyphens only");
    }
}
