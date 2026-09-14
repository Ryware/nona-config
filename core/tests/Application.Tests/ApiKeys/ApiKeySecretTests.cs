using Nona.Application.Common;

namespace Nona.Application.Tests.ApiKeys;

public class ApiKeySecretTests
{
    [Test]
    public async Task Hash_ReturnsUppercaseSha256Digest()
    {
        var hash = ApiKeySecret.Hash("api-key");

        await Assert.That(hash)
            .IsEqualTo("8C284055DBB54B7F053A2DC612C3727C7AA36354361055F2110F4903EA8EE29C");
    }

    [Test]
    public async Task Fingerprint_ReturnsLastEightCharacters()
    {
        var fingerprint = ApiKeySecret.Fingerprint(
            "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF");

        await Assert.That(fingerprint).IsEqualTo("89ABCDEF");
    }
}
