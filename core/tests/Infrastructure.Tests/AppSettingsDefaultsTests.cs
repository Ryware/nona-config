using Microsoft.Extensions.Configuration;
using Nona.Infrastructure.Tests.Common;

namespace Nona.Infrastructure.Tests;

public class AppSettingsDefaultsTests
{
    [Test]
    public async Task AppSettings_DefaultsKeepManagedLibsqlStateUnderVarLibNona()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(TestPaths.ResolveWebApiWorkingDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        await Assert.That(configuration["Storage:Type"]).IsEqualTo("Libsql");
        await Assert.That(configuration["Storage:Libsql:ManagedPrimary:Enabled"]).IsEqualTo("true");
        await Assert.That(configuration["Storage:Libsql:ManagedPrimary:DatabasePath"]).IsEqualTo("/var/lib/nona/primary.db");
        await Assert.That(configuration["Storage:Libsql:ManagedPrimary:WorkingDirectory"]).IsEqualTo("/var/lib/nona/");
        await Assert.That(string.IsNullOrWhiteSpace(configuration["ConnectionStrings:Libsql"])).IsTrue();
    }
}
