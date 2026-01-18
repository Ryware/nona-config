using Nona.Application.Admin.ConfigEntries.Commands;

namespace Nona.Application.Admin.ConfigEntries.Validators;

public class UpsertConfigEntryRequestValidator : AbstractValidator<UpsertConfigEntryRequest>
{
    private static readonly string[] ValidScopes = ["client", "server", "all"];

    public UpsertConfigEntryRequestValidator()
    {
        RuleFor(x => x.Value)
            .NotNull()
            .WithMessage("Value is required");

        RuleFor(x => x.Scope)
            .Must(scope => scope is null || ValidScopes.Contains(scope.ToLowerInvariant()))
            .When(x => x.Scope is not null)
            .WithMessage("Scope must be 'client', 'server', or 'all'");
    }
}
