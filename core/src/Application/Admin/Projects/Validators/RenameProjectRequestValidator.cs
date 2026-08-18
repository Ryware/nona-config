using Nona.Application.Admin.Projects.Commands;

namespace Nona.Application.Admin.Projects.Validators;

public class RenameProjectRequestValidator : AbstractValidator<RenameProjectRequest>
{
    public RenameProjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .Matches(@"^[a-zA-Z0-9-]+$")
            .WithMessage("Name must be alphanumeric with hyphens only");
    }
}
