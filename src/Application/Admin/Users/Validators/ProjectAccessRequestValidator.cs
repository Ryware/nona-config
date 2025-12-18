using Nona.Application.Admin.Users.Commands;

namespace Nona.Application.Admin.Users.Validators;

public class ProjectAccessRequestValidator : AbstractValidator<ProjectAccessRequest>
{
    private static readonly string[] ValidRoles = ["viewer", "admin"];

    public ProjectAccessRequestValidator()
    {
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required")
            .Must(role => ValidRoles.Contains(role.ToLowerInvariant()))
            .WithMessage("Role must be 'viewer' or 'admin'");
    }
}
