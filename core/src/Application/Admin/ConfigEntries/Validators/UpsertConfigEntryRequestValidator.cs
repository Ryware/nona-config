using Nona.Application.Admin.ConfigEntries.Commands;
using Nona.Application.Common;

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

        RuleFor(x => x.ContentType)
            .Must(contentType => string.IsNullOrWhiteSpace(contentType) || ConfigEntryContentTypes.Normalize(contentType) is not null)
            .WithMessage($"Content type must be one of: {string.Join(", ", ConfigEntryContentTypes.LogicalTypes)}.");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description is not null)
            .WithMessage("Description must be 500 characters or fewer.");

        RuleFor(x => x.Unit)
            .MaximumLength(32)
            .When(x => x.Unit is not null)
            .WithMessage("Unit must be 32 characters or fewer.");

        RuleFor(x => x)
            .Must(request => string.IsNullOrWhiteSpace(request.Unit)
                || ConfigEntryContentTypes.Normalize(request.ContentType) is "number")
            .When(x => x.Unit is not null && x.ContentType is not null)
            .WithMessage("Unit is only supported for number parameters.");

        RuleFor(x => x)
            .Custom((request, context) =>
            {
                var contentType = ConfigEntryContentTypes.Normalize(request.ContentType);
                if (contentType is null)
                    return;

                if (!ConfigEntryContentTypes.IsValidValue(request.Value, contentType, out var error))
                    context.AddFailure(nameof(UpsertConfigEntryRequest.Value), error!);
            });
    }
}
