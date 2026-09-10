using Nona.Application.Admin.Common;
using Nona.Application.Admin.ConfigReleases.Commands;
using Nona.Application.Admin.ConfigReleases.DTOs;
using Nona.Application.Common;
using Nona.Domain;
using Nona.Domain.Enums;

namespace Nona.Application.Admin.ConfigReleases.Validators;

public class PublishConfigReleaseRequestValidator : AbstractValidator<PublishConfigReleaseRequest>
{
    public PublishConfigReleaseRequestValidator()
    {
        RuleFor(request => request.Version)
            .NotEmpty()
            .Must(BeExactReleaseVersion)
            .WithMessage("Version must use major.minor.patch format.");

        RuleFor(request => request)
            .Custom((request, context) =>
            {
                if (request.Entries is null)
                    return;

                var validation = PublishConfigReleaseEntryPayloadValidation.Validate(request.Entries);
                foreach (var failure in validation.Failures)
                    context.AddFailure(failure.PropertyName, failure.ErrorMessage);
            });
    }

    private static bool BeExactReleaseVersion(string version)
    {
        return ConfigReleaseVersions.TryParseExact(version, out _);
    }
}

internal sealed record ValidatedPublishConfigReleaseEntry(
    string Key,
    string Value,
    string ContentType,
    KeyScope Scope,
    string? Description,
    string? Unit);

internal sealed record PublishConfigReleaseEntryValidationFailure(
    string PropertyName,
    string ErrorMessage);

internal sealed record PublishConfigReleaseEntryPayloadValidationResult(
    IReadOnlyList<ValidatedPublishConfigReleaseEntry> Entries,
    IReadOnlyList<PublishConfigReleaseEntryValidationFailure> Failures);

internal static class PublishConfigReleaseEntryPayloadValidation
{
    public static PublishConfigReleaseEntryPayloadValidationResult Validate(
        IReadOnlyList<ConfigReleaseEntryDto> entries)
    {
        var validatedEntries = new List<ValidatedPublishConfigReleaseEntry>(entries.Count);
        var failures = new List<PublishConfigReleaseEntryValidationFailure>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < entries.Count; index++)
        {
            var propertyName = $"Entries[{index}]";
            var entry = entries[index];
            if (entry is null)
            {
                failures.Add(new(propertyName, "Release entry is required."));
                continue;
            }

            var isValid = true;
            if (!ValidationHelpers.IsValidKey(entry.Key))
            {
                var error = string.IsNullOrWhiteSpace(entry.Key)
                    ? "Release entries must have a key."
                    : ConfigEntryKey.ValidationError;
                failures.Add(new($"{propertyName}.Key", error));
                isValid = false;
            }
            else if (!keys.Add(entry.Key))
            {
                failures.Add(new($"{propertyName}.Key", "Release entry keys must be unique (case-insensitive)."));
                isValid = false;
            }

            if (entry.Value is null)
            {
                failures.Add(new($"{propertyName}.Value", "Value is required."));
                isValid = false;
            }

            var scope = EnumExtensions.ParseKeyScope(entry.Scope);
            if (scope is null)
            {
                failures.Add(new($"{propertyName}.Scope", "Invalid scope. Must be 'client', 'server', or 'all'."));
                isValid = false;
            }

            var contentType = ConfigEntryContentTypes.Normalize(entry.ContentType);
            if (!string.IsNullOrWhiteSpace(entry.ContentType) && contentType is null)
            {
                failures.Add(new(
                    $"{propertyName}.ContentType",
                    $"Content type must be one of: {string.Join(", ", ConfigEntryContentTypes.LogicalTypes)}."));
                isValid = false;
            }
            else if (entry.Value is not null)
            {
                contentType ??= ConfigEntryContentTypes.Infer(entry.Value);
                if (!ConfigEntryContentTypes.IsValidValue(entry.Value, contentType, out var error))
                {
                    failures.Add(new($"{propertyName}.Value", error!));
                    isValid = false;
                }
            }

            var description = ConfigEntryMetadata.NormalizeDescription(entry.Description);
            if (description?.Length > ConfigEntryMetadata.MaxDescriptionLength)
            {
                failures.Add(new(
                    $"{propertyName}.Description",
                    $"Description must be {ConfigEntryMetadata.MaxDescriptionLength} characters or fewer."));
                isValid = false;
            }

            var unit = ConfigEntryMetadata.NormalizeUnit(entry.Unit);
            if (unit?.Length > ConfigEntryMetadata.MaxUnitLength)
            {
                failures.Add(new(
                    $"{propertyName}.Unit",
                    $"Unit must be {ConfigEntryMetadata.MaxUnitLength} characters or fewer."));
                isValid = false;
            }

            if (unit is not null && contentType is not "number")
            {
                failures.Add(new($"{propertyName}.Unit", "Unit is only supported for number parameters."));
                isValid = false;
            }

            if (isValid)
            {
                validatedEntries.Add(new(
                    entry.Key,
                    entry.Value!,
                    contentType!,
                    scope!.Value,
                    description,
                    unit));
            }
        }

        return new(validatedEntries, failures);
    }
}
