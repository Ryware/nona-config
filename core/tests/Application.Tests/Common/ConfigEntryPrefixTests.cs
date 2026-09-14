using Nona.Domain;

namespace Nona.Application.Tests.Common;

public class ConfigEntryPrefixTests
{
    [Test]
    public async Task AcceptsEmptyAndKeyCharacterFragments()
    {
        foreach (var prefix in new string?[]
                 {
                     null,
                     "",
                     "GroupA:",
                     "feature.v2_",
                     "feature-",
                     ".",
                     "_",
                     "-",
                     ":",
                     "123"
                 })
        {
            await Assert.That(ConfigEntryPrefix.IsValid(prefix)).IsTrue();
        }
    }

    [Test]
    public async Task RejectsCharactersOutsideTheKeyCharacterSet()
    {
        foreach (var prefix in new[]
                 {
                     "Ång",
                     "不存在",
                     "%",
                     "Group A",
                     "Group/A",
                     "Group\\A",
                     "Group\tA",
                     "Group\nA"
                 })
        {
            await Assert.That(ConfigEntryPrefix.IsValid(prefix)).IsFalse();
        }
    }

    [Test]
    public async Task NormalizesAndMatchesAsciiWithoutUnicodeOverfolding()
    {
        await Assert.That(ConfigEntryPrefix.Normalize("groupa:")).IsEqualTo("GROUPA:");
        await Assert.That(ConfigEntryPrefix.StartsWith("GroupA:One", "groupa:")).IsTrue();
        await Assert.That(ConfigEntryPrefix.StartsWith("S:Flag", "ſ")).IsFalse();
        await Assert.That(ConfigEntryPrefix.StartsWith("ſ:Legacy", "S")).IsFalse();
        await Assert.That(ConfigEntryPrefix.StartsWith("Ångström", "Ång")).IsTrue();
        await Assert.That(ConfigEntryPrefix.StartsWith("Ångström", "ång")).IsFalse();
    }
}
