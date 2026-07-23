using Nona.Domain;

namespace Nona.Application.Tests.Common;

public class ConfigEntryKeyTests
{
    [Test]
    public async Task AllowsAsciiLettersDigitsAndSupportedSeparators()
    {
        foreach (var key in new[]
                 {
                     "feature",
                     "Feature2",
                     "feature.enabled",
                     "feature_flag",
                     "feature-flag",
                     "feature:value",
                     "Features:Checkout",
                     "Features:Example",
                     "feature.v2_flag-enabled"
                 })
        {
            await Assert.That(ConfigEntryKey.IsValid(key)).IsTrue();
        }
    }

    [Test]
    public async Task RejectsKeysOutsideTheSupportedAsciiCharacterSet()
    {
        foreach (var key in new string?[]
                 {
                     null,
                     "",
                     " ",
                     ".",
                     ":",
                     "___",
                     "---",
                     "feature flag",
                     "feature/value",
                     "feature@value",
                     "feature?value",
                     "feature#value",
                     "Ångström",
                     "不存在",
                     "feature\tvalue",
                     "feature\nvalue"
                 })
        {
            await Assert.That(ConfigEntryKey.IsValid(key)).IsFalse();
        }
    }
}
