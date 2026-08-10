using Nona.Application.Admin.Users.Commands;

namespace Nona.Application.Admin.Users.Validators;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    private static readonly string[] ValidRoles = ["admin", "member"];
    private static readonly string[] ValidScopes = ["client", "server", "all"];

    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .When(x => x.Name is not null)
            .WithMessage("Name is required");

        RuleFor(x => x.Role)
            .Must(role => role is null || ValidRoles.Contains(role.ToLowerInvariant()))
            .When(x => x.Role is not null)
            .WithMessage("Role must be 'admin' or 'member'");

        RuleFor(x => x.Scope)
            .Must(scope => scope is null || ValidScopes.Contains(scope.ToLowerInvariant()))
            .When(x => x.Scope is not null)
            .WithMessage("Scope must be 'client', 'server', or 'all'");
    }
}
