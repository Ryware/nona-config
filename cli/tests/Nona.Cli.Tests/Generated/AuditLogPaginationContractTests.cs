using CliAuditLogPage = Nona.Cli.Generated.Models.AuditLogPageDto;
using CliAuditLogQuery = Nona.Cli.Generated.Admin.AuditLogs.AuditLogsRequestBuilder.AuditLogsRequestBuilderGetQueryParameters;
using MigratorAuditLogPage = Nona.Migrator.Core.Generated.Models.AuditLogPageDto;
using MigratorAuditLogQuery = Nona.Migrator.Core.Generated.Admin.AuditLogs.AuditLogsRequestBuilder.AuditLogsRequestBuilderGetQueryParameters;

namespace Nona.Cli.Tests.Generated;

public sealed class AuditLogPaginationContractTests
{
    [Test]
    public async Task GeneratedAuditPaginationPropertiesAcceptIntegers()
    {
        var cliQuery = new CliAuditLogQuery
        {
            Page = 2,
            PageSize = 25
        };
        var migratorQuery = new MigratorAuditLogQuery
        {
            Page = 3,
            PageSize = 50
        };
        var cliPage = new CliAuditLogPage
        {
            Page = 2,
            PageSize = 25,
            TotalCount = 101,
            TotalPages = 5
        };
        var migratorPage = new MigratorAuditLogPage
        {
            Page = 3,
            PageSize = 50,
            TotalCount = 151,
            TotalPages = 4
        };

        await Assert.That(cliQuery.Page).IsEqualTo(2);
        await Assert.That(cliQuery.PageSize).IsEqualTo(25);
        await Assert.That(migratorQuery.Page).IsEqualTo(3);
        await Assert.That(migratorQuery.PageSize).IsEqualTo(50);
        await Assert.That(cliPage.Page).IsEqualTo(2);
        await Assert.That(cliPage.PageSize).IsEqualTo(25);
        await Assert.That(cliPage.TotalCount).IsEqualTo(101);
        await Assert.That(cliPage.TotalPages).IsEqualTo(5);
        await Assert.That(migratorPage.Page).IsEqualTo(3);
        await Assert.That(migratorPage.PageSize).IsEqualTo(50);
        await Assert.That(migratorPage.TotalCount).IsEqualTo(151);
        await Assert.That(migratorPage.TotalPages).IsEqualTo(4);
    }
}
