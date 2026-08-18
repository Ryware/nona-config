using Nona.Application.Admin.ParameterShareLinks.Queries;
using Nona.Application.Tests.Common;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.ParameterShareLinks;

public class ListParameterShareLinksQueryTests
{
    private const string ProjectName = "test-project";
    private const string EnvironmentName = "production";
    private const string ConfigKey = "API_URL";

    [Test]
    public async Task ProjectViewer_CannotListShareLinks()
    {
        var fixture = new TestFixture();
        fixture.SetupAsProjectUser("viewer", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        var repository = Substitute.For<IParameterShareLinkRepository>();
        var handler = CreateHandler(fixture, repository);

        var result = await handler.Handle(
            new ListParameterShareLinksQuery(ProjectName, EnvironmentName, ConfigKey),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
        await repository.DidNotReceive().ListByConfigEntryAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProjectEditor_CanListShareLinks()
    {
        var fixture = new TestFixture();
        fixture.SetupAsProjectAdmin("editor", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey);
        var repository = Substitute.For<IParameterShareLinkRepository>();
        repository.ListByConfigEntryAsync(
                ProjectName,
                EnvironmentName,
                ConfigKey,
                Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await CreateHandler(fixture, repository).Handle(
            new ListParameterShareLinksQuery(ProjectName, EnvironmentName, ConfigKey),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ShareLinks).IsEmpty();
    }

    private static ListParameterShareLinksQueryHandler CreateHandler(
        TestFixture fixture,
        IParameterShareLinkRepository repository) => new(
            fixture.ProjectRepository,
            fixture.ConfigEntryRepository,
            repository,
            fixture.ProjectAccessService);
}
