using Nona.Application.Admin.ParameterShareLinks.Commands;
using Nona.Application.Common.Interfaces;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.ParameterShareLinks;

public class RevokeParameterShareLinkCommandTests
{
    private const string ProjectName = "test-project";
    private const string EnvironmentName = "production";
    private const string ConfigKey = "API_URL";

    [Test]
    public async Task RevokeShareLink_MarksLinkRevokedAndAudits()
    {
        var now = new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc);
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.DateTime.NowUtc.Returns(now);
        fixture.SetupProjectExists(ProjectName);

        var shareLinkRepository = Substitute.For<IParameterShareLinkRepository>();
        var auditLogService = Substitute.For<IAuditLogService>();
        shareLinkRepository.GetByIdAsync(7, Arg.Any<CancellationToken>())
            .Returns(new ParameterShareLink
            {
                Id = 7,
                TokenHash = new string('A', 64),
                Project = ProjectName,
                Environment = EnvironmentName,
                Key = ConfigKey,
                CanEdit = true,
                CreatedBy = "admin@example.com",
                CreatedAt = now,
                ExpiresAt = now.AddHours(1)
            });

        var handler = new RevokeParameterShareLinkCommandHandler(
            fixture.ProjectRepository,
            shareLinkRepository,
            fixture.ProjectAccessService,
            fixture.DateTime,
            auditLogService);

        var result = await handler.Handle(
            new RevokeParameterShareLinkCommand(ProjectName, EnvironmentName, ConfigKey, 7),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await shareLinkRepository.Received(1).RevokeAsync(7, now, Arg.Any<CancellationToken>());
        await auditLogService.Received(1).WriteAsync(
            "Share Link Revoked",
            ConfigKey,
            ProjectName,
            EnvironmentName,
            Arg.Any<CancellationToken>());
    }
}
