using Nona.Application.Admin.ConfigReleases.Commands;

namespace Nona.Application.Admin.ConfigReleases.Validators;

public class SetActiveConfigReleaseRequestValidator : AbstractValidator<SetActiveConfigReleaseRequest>
{
    public SetActiveConfigReleaseRequestValidator()
    {
        RuleFor(request => request.Version)
            .Must(version => string.IsNullOrWhiteSpace(version) || ConfigReleaseVersions.TryParseExact(version, out _))
            .WithMessage("Version must use major.minor.patch format.");
    }
}
