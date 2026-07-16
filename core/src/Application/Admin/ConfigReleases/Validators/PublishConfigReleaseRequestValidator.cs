using Nona.Application.Admin.ConfigReleases.Commands;

namespace Nona.Application.Admin.ConfigReleases.Validators;

public class PublishConfigReleaseRequestValidator : AbstractValidator<PublishConfigReleaseRequest>
{
    public PublishConfigReleaseRequestValidator()
    {
        RuleFor(request => request.Version)
            .NotEmpty()
            .Must(BeExactReleaseVersion)
            .WithMessage("Version must use major.minor.patch format.");
    }

    private static bool BeExactReleaseVersion(string version)
    {
        return ConfigReleaseVersions.TryParseExact(version, out _);
    }
}
