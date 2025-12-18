using Nona.Application.Admin.Projects.Commands;

namespace Nona.Application.Admin.Projects.Validators;

public class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Slug is required")
            .Matches(@"^[a-zA-Z0-9-]+$")
            .WithMessage("Slug must be alphanumeric with hyphens only");
    }
}
