using Nona.Application.Admin.Environments.Commands;
using Nona.Application.Tests.Common;
using NSubstitute;

namespace Nona.Application.Tests.Environments;

public class RenameEnvironmentCommandTests
{
    private const string ProjectName = "test-project";
    private const string CurrentName = "development";
    private const string NewName = "staging";

    [Test]
    public async Task ProjectEditor_CanRenameEnvironment()
    {
        var fixture = CreateFixture();
        fixture.SetupAsProjectAdmin("editor", ProjectName);

        var result = await CreateHandler(fixture).Handle(
            new RenameEnvironmentCommand(ProjectName, CurrentName, NewName),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Environment!.Name).IsEqualTo(NewName);
        await fixture.EnvironmentRepository.Received(1).RenameAsync(
            ProjectName,
            CurrentName,
            NewName,
            fixture.DateTime.NowUtc,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Viewer_CannotRenameEnvironment()
    {
        var fixture = CreateFixture();
        fixture.SetupAsProjectUser("viewer", ProjectName);

        var result = await CreateHandler(fixture).Handle(
            new RenameEnvironmentCommand(ProjectName, CurrentName, NewName),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
        await fixture.EnvironmentRepository.DidNotReceiveWithAnyArgs()
            .RenameAsync(default!, default!, default!, default, default);
    }

    [Test]
    public async Task MissingEnvironment_CannotBeRenamed()
    {
        var fixture = CreateFixture(environmentExists: false);
        fixture.SetupAsSystemAdmin();

        var result = await CreateHandler(fixture).Handle(
            new RenameEnvironmentCommand(ProjectName, CurrentName, NewName),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Environment not found");
    }

    [Test]
    public async Task DuplicateEnvironmentName_IsRejected()
    {
        var fixture = CreateFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupEnvironmentExists(ProjectName, NewName);

        var result = await CreateHandler(fixture).Handle(
            new RenameEnvironmentCommand(ProjectName, CurrentName, NewName),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Environment already exists");
    }

    [Test]
    public async Task CaseOnlyRename_IsAllowed()
    {
        var fixture = CreateFixture();
        fixture.SetupAsSystemAdmin();

        var result = await CreateHandler(fixture).Handle(
            new RenameEnvironmentCommand(ProjectName, CurrentName, "Development"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await fixture.EnvironmentRepository.DidNotReceive()
            .ExistsAsync(ProjectName, "Development", Arg.Any<CancellationToken>());
    }

    private static TestFixture CreateFixture(bool environmentExists = true)
    {
        var fixture = new TestFixture();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, CurrentName, environmentExists);
        fixture.SetupEnvironmentExists(ProjectName, NewName, exists: false);
        return fixture;
    }

    private static RenameEnvironmentCommandHandler CreateHandler(TestFixture fixture)
    {
        return new RenameEnvironmentCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);
    }
}
