using Nona.Application.Admin.AuditLogs.Queries;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.AuditLogs;

public class ListAuditLogsQueryTests
{
    [Test]
    public async Task ListAuditLogs_ReturnsPersistedActionKindBeforeAction()
    {
        var repository = Substitute.For<IAuditLogRepository>();
        repository.ListAsync(Arg.Any<AuditLogPageRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new AuditLogPageResult(
                    [
                        new AuditLogEntry
                        {
                            Id = 12,
                            Actor = "audit.user@example.test",
                            ActorIsSystem = false,
                            ActionKind = AuditActionKind.Create,
                            Action = "Published Config Release",
                            Target = "1.3.1",
                            Project = "sample-project",
                            Environment = "production",
                            CreatedAt = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc)
                        }
                    ],
                    1,
                    ["Published Config Release"],
                    ["production"]));

        var handler = new ListAuditLogsQueryHandler(repository);
        var result = await handler.Handle(new ListAuditLogsQuery(), CancellationToken.None);

        await Assert.That(result.Items).Count().IsEqualTo(1);
        await Assert.That(result.Items[0].ActionKind).IsEqualTo("create");
        await Assert.That(result.Items[0].Action).IsEqualTo("Published Config Release");
        await Assert.That(result.TotalCount).IsEqualTo(1);
        await Assert.That(result.TotalPages).IsEqualTo(1);
    }

    [Test]
    public async Task ListAuditLogs_NormalizesFiltersAndBuildsRepositoryPageRequest()
    {
        var repository = Substitute.For<IAuditLogRepository>();
        AuditLogPageRequest? captured = null;
        repository.ListAsync(Arg.Do<AuditLogPageRequest>(request => captured = request), Arg.Any<CancellationToken>())
            .Returns(new AuditLogPageResult([], 0, ["Updated Project"], ["production"]));
        var handler = new ListAuditLogsQueryHandler(repository);

        var result = await handler.Handle(
            new ListAuditLogsQuery(
                Page: 3,
                PageSize: 250,
                Search: "  needle ",
                Action: " Updated Project ",
                Environment: " production ",
                DateFrom: new DateOnly(2026, 7, 1),
                DateTo: new DateOnly(2026, 7, 31)),
            CancellationToken.None);

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Offset).IsEqualTo(200);
        await Assert.That(captured.Limit).IsEqualTo(ListAuditLogsQueryHandler.MaximumPageSize);
        await Assert.That(captured.Filter.Search).IsEqualTo("needle");
        await Assert.That(captured.Filter.Action).IsEqualTo("Updated Project");
        await Assert.That(captured.Filter.Environment).IsEqualTo("production");
        await Assert.That(captured.Filter.CreatedFrom).IsEqualTo(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        await Assert.That(captured.Filter.CreatedToExclusive).IsEqualTo(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        await Assert.That(result.Page).IsEqualTo(3);
        await Assert.That(result.PageSize).IsEqualTo(100);
    }
}
