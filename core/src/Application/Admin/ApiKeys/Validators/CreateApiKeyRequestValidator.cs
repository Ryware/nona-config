using Nona.Application.Admin.ApiKeys.Commands;

namespace Nona.Application.Admin.ApiKeys.Validators;

public class CreateApiKeyRequestValidator : AbstractValidator<CreateApiKeyRequest>
{
    private static readonly string[] ValidScopes = ["client", "server", "all"];

    public CreateApiKeyRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required")
            .MaximumLength(100)
            .WithMessage("Name must be 100 characters or fewer");

        RuleFor(x => x.Environment)
            .MaximumLength(100)
            .When(x => x.Environment is not null)
            .WithMessage("Environment must be 100 characters or fewer");

        RuleFor(x => x.Scope)
            .Must(scope => scope is null || ValidScopes.Contains(scope.Trim().ToLowerInvariant()))
            .When(x => x.Scope is not null)
            .WithMessage("Scope must be 'client', 'server', or 'all'");
    }
}
