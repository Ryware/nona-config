using Nona.Application.Admin.Users.Commands;

namespace Nona.Application.Admin.Users.Validators;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    private static readonly string[] ValidRoles = ["viewer", "editor"];
    private static readonly string[] ValidScopes = ["client", "server", "all"];

    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required");

        RuleFor(x => x.Role)
            .Must(role => role is null || ValidRoles.Contains(role.ToLowerInvariant()))
            .When(x => x.Role is not null)
            .WithMessage("Role must be 'viewer' or 'editor'");

        RuleFor(x => x.Scope)
            .Must(scope => scope is null || ValidScopes.Contains(scope.ToLowerInvariant()))
            .When(x => x.Scope is not null)
            .WithMessage("Scope must be 'client', 'server', or 'all'");
    }
}
