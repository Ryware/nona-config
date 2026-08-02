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
        repository.ListAsync(Arg.Any<CancellationToken>())
            .Returns(
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
            ]);

        var handler = new ListAuditLogsQueryHandler(repository);
        var result = await handler.Handle(new ListAuditLogsQuery(), CancellationToken.None);

        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].ActionKind).IsEqualTo("create");
        await Assert.That(result[0].Action).IsEqualTo("Published Config Release");
    }
}
