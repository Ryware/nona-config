using Nona.Application.Admin.ParameterShareLinks.Commands;
using Nona.Application.Common.Interfaces;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.ParameterShareLinks;

public class CreateParameterShareLinkCommandTests
{
    private const string ProjectName = "test-project";
    private const string EnvironmentName = "production";
    private const string ConfigKey = "API_URL";

    [Test]
    public async Task CreateShareLink_StoresHashAndReturnsSixteenCharacterToken()
    {
        var now = new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc);
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin("admin@example.com");
        fixture.DateTime.NowUtc.Returns(now);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey);

        var shareLinkRepository = Substitute.For<IParameterShareLinkRepository>();
        var auditLogService = Substitute.For<IAuditLogService>();
        ParameterShareLink? savedLink = null;

        shareLinkRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ParameterShareLink?)null);
        shareLinkRepository
            .AddAsync(Arg.Do<ParameterShareLink>(link =>
            {
                link.Id = 42;
                savedLink = link;
            }), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new CreateParameterShareLinkCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            shareLinkRepository,
            fixture.ProjectAccessService,
            fixture.DateTime,
            fixture.CurrentUserService,
            auditLogService);

        var result = await handler.Handle(
            new CreateParameterShareLinkCommand(ProjectName, EnvironmentName, ConfigKey, null, true),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ShareLink).IsNotNull();
        await Assert.That(result.ShareLink!.Token).Length().IsEqualTo(16);
        await Assert.That(result.ShareLink.ExpiresAt).IsEqualTo(now.AddHours(1));
        await Assert.That(savedLink).IsNotNull();
        await Assert.That(savedLink!.TokenHash).IsNotEqualTo(result.ShareLink.Token);
        await Assert.That(savedLink.TokenHash).Length().IsEqualTo(64);
        await Assert.That(savedLink.Project).IsEqualTo(ProjectName);
        await Assert.That(savedLink.Environment).IsEqualTo(EnvironmentName);
        await Assert.That(savedLink.Key).IsEqualTo(ConfigKey);

        await auditLogService.Received(1).WriteAsync(
            "Share Link Created",
            ConfigKey,
            ProjectName,
            EnvironmentName,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateShareLink_RejectsInvalidExpiration()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey);

        var shareLinkRepository = Substitute.For<IParameterShareLinkRepository>();
        var handler = new CreateParameterShareLinkCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            shareLinkRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var result = await handler.Handle(
            new CreateParameterShareLinkCommand(ProjectName, EnvironmentName, ConfigKey, "2d", true),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Expiration must be one of: 1h, 1d, 3d, 30d, 12m.");
        await shareLinkRepository.DidNotReceive().AddAsync(
            Arg.Any<ParameterShareLink>(),
            Arg.Any<CancellationToken>());
    }
}
