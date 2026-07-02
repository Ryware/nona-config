using Nona.Application.Common.Interfaces;
using Nona.Application.Shared.ParameterShareLinks;
using Nona.Application.Shared.ParameterShareLinks.Commands;
using Nona.Application.Shared.ParameterShareLinks.Queries;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.ParameterShareLinks;

public class SharedParameterCommandTests
{
    private const string Token = "AbCdEf1234567890";
    private const string ProjectName = "test-project";
    private const string EnvironmentName = "production";
    private const string ConfigKey = "API_URL";

    private readonly DateTime _now = new(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task GetSharedParameter_ReturnsSingleScopedParameterAndAuditsAccess()
    {
        var shareLinkRepository = Substitute.For<IParameterShareLinkRepository>();
        var configEntryRepository = Substitute.For<IConfigEntryRepository>();
        var dateTime = Substitute.For<IDateTime>();
        var auditLogService = Substitute.For<IAuditLogService>();
        dateTime.NowUtc.Returns(_now);
        shareLinkRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(EditableLink());
        configEntryRepository.GetAsync(ProjectName, EnvironmentName, ConfigKey, Arg.Any<CancellationToken>())
            .Returns(Entry("https://api.example.com"));

        var handler = new GetSharedParameterQueryHandler(
            shareLinkRepository,
            configEntryRepository,
            dateTime,
            auditLogService);

        var result = await handler.Handle(new GetSharedParameterQuery(Token), CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Parameter).IsNotNull();
        await Assert.That(result.Parameter!.Environment).IsEqualTo(EnvironmentName);
        await Assert.That(result.Parameter.Key).IsEqualTo(ConfigKey);
        await Assert.That(result.Parameter.Value).IsEqualTo("https://api.example.com");

        await auditLogService.Received(1).WriteAsAsync(
            "Shared link #7",
            true,
            "Share Link Accessed",
            ConfigKey,
            ProjectName,
            EnvironmentName,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetSharedParameter_RejectsExpiredLinkBeforeLoadingParameter()
    {
        var shareLinkRepository = Substitute.For<IParameterShareLinkRepository>();
        var configEntryRepository = Substitute.For<IConfigEntryRepository>();
        var dateTime = Substitute.For<IDateTime>();
        dateTime.NowUtc.Returns(_now);
        shareLinkRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(EditableLink(expiresAt: _now.AddTicks(-1)));

        var handler = new GetSharedParameterQueryHandler(
            shareLinkRepository,
            configEntryRepository,
            dateTime);

        var result = await handler.Handle(new GetSharedParameterQuery(Token), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo(ParameterShareLinkErrorCodes.Expired);
        await configEntryRepository.DidNotReceive().GetAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetSharedParameter_RejectsRevokedLinkBeforeLoadingParameter()
    {
        var shareLinkRepository = Substitute.For<IParameterShareLinkRepository>();
        var configEntryRepository = Substitute.For<IConfigEntryRepository>();
        var dateTime = Substitute.For<IDateTime>();
        var revokedLink = EditableLink();
        revokedLink.RevokedAt = _now;
        dateTime.NowUtc.Returns(_now);
        shareLinkRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(revokedLink);

        var handler = new GetSharedParameterQueryHandler(
            shareLinkRepository,
            configEntryRepository,
            dateTime);

        var result = await handler.Handle(new GetSharedParameterQuery(Token), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo(ParameterShareLinkErrorCodes.Revoked);
        await configEntryRepository.DidNotReceive().GetAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateSharedParameter_RejectsViewOnlyLink()
    {
        var shareLinkRepository = Substitute.For<IParameterShareLinkRepository>();
        var configEntryRepository = Substitute.For<IConfigEntryRepository>();
        var dateTime = Substitute.For<IDateTime>();
        dateTime.NowUtc.Returns(_now);
        shareLinkRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(EditableLink(canEdit: false));

        var handler = new UpdateSharedParameterCommandHandler(
            shareLinkRepository,
            configEntryRepository,
            dateTime);

        var result = await handler.Handle(
            new UpdateSharedParameterCommand(Token, "https://shared.example.com"),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo(ParameterShareLinkErrorCodes.ViewOnly);
        await configEntryRepository.DidNotReceive().AddVersionAsync(
            Arg.Any<ConfigEntry>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateSharedParameter_AddsVersionWithSharedLinkActorAndAudits()
    {
        var shareLinkRepository = Substitute.For<IParameterShareLinkRepository>();
        var configEntryRepository = Substitute.For<IConfigEntryRepository>();
        var dateTime = Substitute.For<IDateTime>();
        var auditLogService = Substitute.For<IAuditLogService>();
        ConfigEntry? savedEntry = null;

        dateTime.NowUtc.Returns(_now);
        shareLinkRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(EditableLink());
        configEntryRepository.GetAsync(ProjectName, EnvironmentName, ConfigKey, Arg.Any<CancellationToken>())
            .Returns(Entry("https://api.example.com"));
        configEntryRepository
            .AddVersionAsync(
                Arg.Do<ConfigEntry>(entry => savedEntry = entry),
                "Shared link #7",
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<ConfigEntry>(0));

        var handler = new UpdateSharedParameterCommandHandler(
            shareLinkRepository,
            configEntryRepository,
            dateTime,
            auditLogService);

        var result = await handler.Handle(
            new UpdateSharedParameterCommand(Token, "https://shared.example.com"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(savedEntry).IsNotNull();
        await Assert.That(savedEntry!.Project).IsEqualTo(ProjectName);
        await Assert.That(savedEntry.Environment).IsEqualTo(EnvironmentName);
        await Assert.That(savedEntry.Key).IsEqualTo(ConfigKey);
        await Assert.That(savedEntry.Value).IsEqualTo("https://shared.example.com");
        await Assert.That(savedEntry.UpdatedAt).IsEqualTo(_now);

        await auditLogService.Received(1).WriteAsAsync(
            "Shared link #7",
            true,
            "Parameter Updated Via Shared Link",
            ConfigKey,
            ProjectName,
            EnvironmentName,
            Arg.Any<CancellationToken>());
    }

    private ParameterShareLink EditableLink(bool canEdit = true, DateTime? expiresAt = null)
    {
        return new ParameterShareLink
        {
            Id = 7,
            TokenHash = new string('A', 64),
            Project = ProjectName,
            Environment = EnvironmentName,
            Key = ConfigKey,
            CanEdit = canEdit,
            CreatedBy = "admin@example.com",
            CreatedAt = _now,
            ExpiresAt = expiresAt ?? _now.AddHours(1)
        };
    }

    private ConfigEntry Entry(string value)
    {
        return new ConfigEntry
        {
            Project = ProjectName,
            Environment = EnvironmentName,
            Key = ConfigKey,
            Value = value,
            ContentType = "text",
            Scope = KeyScope.All,
            CreatedAt = _now.AddDays(-1),
            UpdatedAt = _now.AddDays(-1)
        };
    }
}
