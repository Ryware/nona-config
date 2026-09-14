using Nona.Application.Admin.ConfigEntries.Queries;
using Nona.Application.Tests.Common;
using Nona.Domain;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using NSubstitute;

namespace Nona.Application.Tests.ConfigEntries;

public class GetConfigEntriesQueryTests
{
    [Test]
    public async Task InvalidPrefix_IsRejectedBeforeRepositoryAccess()
    {
        var fixture = new TestFixture();
        var handler = new GetConfigEntriesQueryHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var result = await handler.Handle(
            new GetConfigEntriesQuery("test-project", "development", "Group%"),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo(ConfigEntryPrefix.ValidationError);
        await Assert.That(fixture.ProjectRepository.ReceivedCalls()).IsEmpty();
        await Assert.That(fixture.EnvironmentRepository.ReceivedCalls()).IsEmpty();
        await Assert.That(fixture.ConfigEntryRepository.ReceivedCalls()).IsEmpty();
    }

    [Test]
    public async Task Prefix_IsForwardedToRepository()
    {
        const string project = "test-project";
        const string environment = "development";
        const string prefix = "GroupA:";
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(project);
        fixture.SetupEnvironmentExists(project, environment);
        fixture.ConfigEntryRepository.ListAsync(
                project,
                environment,
                prefix,
                Arg.Any<CancellationToken>())
            .Returns([
                new ConfigEntry
                {
                    Project = project,
                    Environment = environment,
                    Key = "GroupA:One",
                    Value = "true",
                    ContentType = "boolean",
                    Scope = KeyScope.Frontend
                }
            ]);

        var handler = new GetConfigEntriesQueryHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var result = await handler.Handle(
            new GetConfigEntriesQuery(project, environment, prefix),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ConfigEntries).Count().IsEqualTo(1);
        await Assert.That(result.ConfigEntries![0].Key).IsEqualTo("GroupA:One");
        await fixture.ConfigEntryRepository.Received(1).ListAsync(
            project,
            environment,
            prefix,
            Arg.Any<CancellationToken>());
    }
}
