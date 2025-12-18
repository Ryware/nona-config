using Nona.Application.Admin.Users.Commands;

namespace Nona.Application.Admin.Users.Validators;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    private static readonly string[] ValidRoles = ["user", "admin"];
    private static readonly string[] ValidScopes = ["client", "server", "all"];

    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Password)
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters")
            .When(x => x.Password is not null);

        RuleFor(x => x.Role)
            .Must(role => role is null || ValidRoles.Contains(role.ToLowerInvariant()))
            .When(x => x.Role is not null)
            .WithMessage("Role must be 'user' or 'admin'");

        RuleFor(x => x.Scope)
            .Must(scope => scope is null || ValidScopes.Contains(scope.ToLowerInvariant()))
            .When(x => x.Scope is not null)
            .WithMessage("Scope must be 'client', 'server', or 'all'");
    }
}
