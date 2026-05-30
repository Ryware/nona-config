using Nona.Cli.Config.Commands;
using Nona.Cli.Config.Queries;
using static Nona.Cli.Tests.TestHelpers;

namespace Nona.Cli.Tests.Config;

public sealed class ConfigHandlerTests
{
    [Test]
    public async Task SetDefaultCommandHandler_SavesBaseUrl()
    {
        using var file = new TempFile();
        var store = new CliDefaultsStore(file.Path);
        await new SetDefaultCommandHandler(store).HandleAsync(
            new SetDefaultCommand("base-url", "http://new.example.com"), CancellationToken.None);
        await Assert.That(store.Load().BaseUrl).IsEqualTo("http://new.example.com");
    }

    [Test]
    public async Task SetDefaultCommandHandler_SavesProject()
    {
        using var file = new TempFile();
        var store = new CliDefaultsStore(file.Path);
        await new SetDefaultCommandHandler(store).HandleAsync(
            new SetDefaultCommand("project", "my-app"), CancellationToken.None);
        await Assert.That(store.Load().Project).IsEqualTo("my-app");
    }

    [Test]
    public async Task SetDefaultCommandHandler_PreservesOtherDefaults()
    {
        using var file = new TempFile();
        var store = new CliDefaultsStore(file.Path);
        store.Save(new CliDefaults { BaseUrl = "http://x.com", Project = "existing-project" });
        await new SetDefaultCommandHandler(store).HandleAsync(
            new SetDefaultCommand("base-url", "http://new.example.com"), CancellationToken.None);
        var saved = store.Load();
        await Assert.That(saved.BaseUrl).IsEqualTo("http://new.example.com");
        await Assert.That(saved.Project).IsEqualTo("existing-project");
    }

    [Test]
    public async Task ShowDefaultsQueryHandler_ReturnsZero()
    {
        using var file = new TempFile();
        var store = new CliDefaultsStore(file.Path);
        store.Save(new CliDefaults { BaseUrl = "http://x.com", Project = "my-app" });
        var result = await new ShowDefaultsQueryHandler(store)
            .HandleAsync(new ShowDefaultsQuery(), CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }
}
