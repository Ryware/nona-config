using Nona.Migrator.Core.Generated.Models;
using Nona.Migrator.FirebaseRemoteConfig;

namespace Nona.Migrator.FirebaseRemoteConfigMigrator.Tests;

public sealed class FirebaseRemoteConfigMigrationCommandTests
{
    [Test]
    public async Task DescribeException_FormatsTypedApiError()
    {
        var exception = new ErrorResponse
        {
            Error = "project not found",
            ErrorCode = "NOT_FOUND",
            ResponseStatusCode = 404
        };

        var message = FirebaseRemoteConfigMigrationCommand.DescribeException(exception);

        await Assert.That(message).IsEqualTo("Error: project not found (404, NOT_FOUND)");
    }
}
