using Nona.Application.Admin.Environments.Commands;
using Nona.Application.Admin.Environments.Validators;

namespace Nona.Application.Tests.Environments;

public class EnvironmentRequestValidatorTests
{
    [Test]
    public async Task RejectsNamesStartingWithNonAlphanumericCharacter()
    {
        var createResult = await new CreateEnvironmentRequestValidator()
            .ValidateAsync(new CreateEnvironmentRequest("-development"));
        var renameResult = await new RenameEnvironmentRequestValidator()
            .ValidateAsync(new RenameEnvironmentRequest("-development"));

        await Assert.That(createResult.IsValid).IsFalse();
        await Assert.That(renameResult.IsValid).IsFalse();
    }
}
