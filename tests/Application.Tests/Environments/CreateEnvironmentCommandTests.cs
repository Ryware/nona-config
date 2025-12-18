using Nona.Application.Admin.Environments.Commands;
using Nona.Application.Tests.Common;
using NSubstitute;

namespace Nona.Application.Tests.Environments;

public class CreateEnvironmentCommandTests
{
    private const string ProjectName = "test-project";
    private const string EnvironmentName = "development";

    [Test]
    public async Task SystemAdmin_CanCreateEnvironment()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName, exists: false);

        var handler = new CreateEnvironmentCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new CreateEnvironmentCommand(ProjectName, EnvironmentName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Environment).IsNotNull();
        await Assert.That(result.Environment!.Name).IsEqualTo(EnvironmentName);
    }

    [Test]
    public async Task ProjectAdmin_CanCreateEnvironment()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsProjectAdmin("projectadmin", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName, exists: false);

        var handler = new CreateEnvironmentCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new CreateEnvironmentCommand(ProjectName, EnvironmentName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Environment).IsNotNull();
    }

    [Test]
    public async Task ProjectUser_CannotCreateEnvironment()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsProjectUser("regularuser", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName, exists: false);

        var handler = new CreateEnvironmentCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new CreateEnvironmentCommand(ProjectName, EnvironmentName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
        await fixture.EnvironmentRepository.DidNotReceive()
            .AddAsync(Arg.Any<Nona.Domain.Entities.ProjectEnvironment>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UserWithNoAccess_CannotCreateEnvironment()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsUserWithNoProjectAccess("unauthorized", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName, exists: false);

        var handler = new CreateEnvironmentCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new CreateEnvironmentCommand(ProjectName, EnvironmentName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
    }

    [Test]
    public async Task CreateEnvironment_ProjectNotFound_ReturnsFalse()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName, exists: false);

        var handler = new CreateEnvironmentCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new CreateEnvironmentCommand(ProjectName, EnvironmentName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Project not found");
    }

    [Test]
    public async Task CreateEnvironment_EnvironmentAlreadyExists_ReturnsFalse()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName, exists: true);

        var handler = new CreateEnvironmentCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new CreateEnvironmentCommand(ProjectName, EnvironmentName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Environment already exists");
    }
}
