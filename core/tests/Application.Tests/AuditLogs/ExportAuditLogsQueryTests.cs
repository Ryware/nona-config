using Nona.Application.Admin.AuditLogs.Queries;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.AuditLogs;

public class ExportAuditLogsQueryTests
{
    [Test]
    public async Task ExportAuditLogs_StreamsEveryFilteredBatchUsingAStableCursor()
    {
        var repository = Substitute.For<IAuditLogRepository>();
        var createdAt = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);
        var firstBatch = Enumerable.Range(2, ExportAuditLogsQueryHandler.BatchSize)
            .Reverse()
            .Select(id => Entry(id, createdAt))
            .ToList();
        var finalBatch = new[] { Entry(1, createdAt) };
        var requests = new List<AuditLogBatchRequest>();
        repository.ListBatchAsync(
                Arg.Do<AuditLogBatchRequest>(request => requests.Add(request)),
                Arg.Any<CancellationToken>())
            .Returns(firstBatch, finalBatch);
        var handler = new ExportAuditLogsQueryHandler(repository);
        var results = new List<long>();

        await foreach (var item in handler.Handle(
                           new ExportAuditLogsQuery(
                               Search: " needle ",
                               Action: " Updated Parameter ",
                               Environment: " production ",
                               DateFrom: new DateOnly(2026, 7, 1),
                               DateTo: new DateOnly(2026, 7, 31)),
                           CancellationToken.None))
        {
            results.Add(item.Id);
        }

        await Assert.That(results).Count().IsEqualTo(501);
        await Assert.That(results.Distinct()).Count().IsEqualTo(501);
        await Assert.That(requests).Count().IsEqualTo(2);
        await Assert.That(requests[0].Filter.Search).IsEqualTo("needle");
        await Assert.That(requests[1].BeforeCreatedAt).IsEqualTo(createdAt);
        await Assert.That(requests[1].BeforeId).IsEqualTo(2);
    }

    private static AuditLogEntry Entry(long id, DateTime createdAt)
    {
        return new AuditLogEntry
        {
            Id = id,
            Actor = "audit.user@example.test",
            ActorIsSystem = false,
            ActionKind = AuditActionKind.Update,
            Action = "Updated Parameter",
            Target = $"target-{id}",
            Project = "sample-project",
            Environment = "production",
            CreatedAt = createdAt
        };
    }
}
