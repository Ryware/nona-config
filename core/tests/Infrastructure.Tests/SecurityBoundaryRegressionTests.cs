using Nona.Application.Api.ConfigEntries.Queries;
using Nona.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using Nona.Infrastructure.Configuration;
using Nona.Infrastructure.Repositories.Libsql;
using Nona.Infrastructure.Services;
using Nona.Infrastructure.Tests.Common;
using Nona.Libsql;

namespace Nona.Infrastructure.Tests;

public class SecurityBoundaryRegressionTests
{
    [Test]
    [Arguments("InMemory")]
    [Arguments("Sqlite")]
    [Arguments("Libsql")]
    public async Task DeletedResourceCapabilitiesCannotReachReplacement(string storage)
    {
        await using var fixture = await StorageFixture.Create(storage);
        var services = fixture.Services;
        var projects = services.GetRequiredService<IProjectRepository>();
        var environments = services.GetRequiredService<IEnvironmentRepository>();
        var entries = services.GetRequiredService<IConfigEntryRepository>();
        var keys = services.GetRequiredService<IApiKeyRepository>();
        var links = services.GetRequiredService<IParameterShareLinkRepository>();
        await projects.AddAsync(new Project { Name = "Alpha" });
        await environments.AddAsync(new ProjectEnvironment { Project = "Alpha", Name = "Production" });
        await environments.AddAsync(new ProjectEnvironment { Project = "Alpha", Name = "Other" });
        var original = Entry("old");
        await entries.AddAsync(original);
        var oldLink = Link("old");
        await links.AddAsync(oldLink);
        await keys.AddAsync(new ApiKey { Name = "scoped", Project = "Alpha", Environment = "Production", KeyHash = "scoped", Fingerprint = "scoped" });
        await keys.AddAsync(new ApiKey { Name = "other", Project = "Alpha", Environment = "Other", KeyHash = "other", Fingerprint = "other" });
        await keys.AddAsync(new ApiKey { Name = "global", Project = "Alpha", KeyHash = "global", Fingerprint = "global" });

        // A legitimate link reads, writes and records history without changing metadata.
        await Assert.That((await entries.GetSharedAsync(oldLink, DateTime.UtcNow))!.Value).IsEqualTo("old");
        var update = Entry("edited");
        update.Scope = KeyScope.Frontend;
        update.Description = "stale metadata";
        var edited = await entries.UpdateSharedValueAsync(update, oldLink, DateTime.UtcNow);
        await Assert.That(edited!.Scope).IsEqualTo(KeyScope.Backend);
        await Assert.That(edited.Description).IsEqualTo("keep");
        await Assert.That((await entries.ListVersionsAsync("Alpha", "Production", "flag")).Count).IsEqualTo(2);

        // Both single and bulk deletion must clear capabilities, case-insensitively.
        await entries.DeleteAsync("ALPHA", "PRODUCTION", "FLAG");
        await entries.AddAsync(Entry("replacement"));
        await Assert.That(await links.GetByTokenHashAsync("old")).IsNull();
        await Assert.That(await entries.GetSharedAsync(oldLink, DateTime.UtcNow)).IsNull();
        await Assert.That(await entries.UpdateSharedValueAsync(Entry("attack"), oldLink, DateTime.UtcNow)).IsNull();
        await Assert.That((await entries.GetAsync("Alpha", "Production", "flag"))!.Value).IsEqualTo("replacement");
        await Assert.That(await keys.GetByKeyHashAsync("scoped")).IsNotNull();
        var bulkLink = Link("bulk");
        await links.AddAsync(bulkLink);
        await entries.DeleteManyAsync("alpha", "production", ["FLAG"]);
        await Assert.That(await links.GetByTokenHashAsync("bulk")).IsNull();

        // Remove orphan links too, even when the parameter no longer exists.
        await links.AddAsync(Link("orphan"));
        await environments.DeleteAsync("ALPHA", "PRODUCTION");
        await environments.AddAsync(new ProjectEnvironment { Project = "Alpha", Name = "Production" });
        await entries.AddAsync(Entry("new environment"));
        await Assert.That(await keys.GetByKeyHashAsync("scoped")).IsNull();
        await Assert.That(await links.GetByTokenHashAsync("orphan")).IsNull();
        await Assert.That(await keys.GetByKeyHashAsync("other")).IsNotNull();
        await Assert.That(await keys.GetByKeyHashAsync("global")).IsNotNull();
        var fresh = Link("fresh");
        await links.AddAsync(fresh);
        await Assert.That((await entries.GetSharedAsync(fresh, DateTime.UtcNow))!.Value).IsEqualTo("new environment");
        await Assert.That(await entries.UpdateSharedValueAsync(Entry("fresh edit"), fresh, DateTime.UtcNow)).IsNotNull();
        await links.RevokeAsync(fresh.Id, DateTime.UtcNow);
        await Assert.That(await entries.UpdateSharedValueAsync(Entry("revoked"), fresh, DateTime.UtcNow)).IsNull();
        await Assert.That(await entries.GetSharedAsync(fresh, DateTime.UtcNow)).IsNull();
        var viewOnly = Link("view");
        viewOnly.CanEdit = false;
        await links.AddAsync(viewOnly);
        await Assert.That(await entries.GetSharedAsync(viewOnly, DateTime.UtcNow)).IsNotNull();
        await Assert.That(await entries.UpdateSharedValueAsync(Entry("forbidden"), viewOnly, DateTime.UtcNow)).IsNull();
    }

    [Test]
    [Arguments("InMemory")]
    [Arguments("Sqlite")]
    [Arguments("Libsql")]
    public async Task BootstrapHasExactlyOneWinnerAcrossConcurrentClients(string storage)
    {
        await using var fixture = await StorageFixture.Create(storage);
        var users = fixture.Services.GetRequiredService<IUserRepository>();
        using var secondSqliteClient = storage == "Sqlite" ? new SqliteDatabaseClient(fixture.Path) : null;
        using var secondLibsqlClient = fixture.Sqld?.CreateClient();
        IUserRepository second = secondSqliteClient is not null ? new LibsqlUserRepository(secondSqliteClient)
            : secondLibsqlClient is not null ? new LibsqlUserRepository(secondLibsqlClient) : users;
        using var start = new ManualResetEventSlim();
        var attempts = Enumerable.Range(0, 8).Select(i => Task.Run(async () =>
        {
            start.Wait();
            return await (i % 2 == 0 ? users : second).TryAddFirstUserAsync(new User
            {
                Email = $"admin{i}@example.com",
                Name = "Admin",
                Role = UserRole.Admin
            });
        })).ToArray();
        start.Set();
        var results = await Task.WhenAll(attempts);
        await Assert.That(results.Count(success => success)).IsEqualTo(1);
        await Assert.That(await users.CountAsync()).IsEqualTo(1);
        await Assert.That(await users.TryAddFirstUserAsync(new User { Email = "late@example.com", Name = "Late" })).IsFalse();
        await users.AddAsync(new User { Email = "member@example.com", Name = "Member" });
        await Assert.That(await users.CountAsync()).IsEqualTo(2);
    }

    [Test]
    [Arguments("InMemory", "value")]
    [Arguments("InMemory", "all")]
    [Arguments("InMemory", "release")]
    [Arguments("InMemory", "release-all")]
    [Arguments("InMemory", "release-304")]
    [Arguments("Sqlite", "value")]
    [Arguments("Sqlite", "all")]
    [Arguments("Sqlite", "release")]
    [Arguments("Sqlite", "release-all")]
    [Arguments("Sqlite", "release-304")]
    [Arguments("Libsql", "value")]
    [Arguments("Libsql", "all")]
    [Arguments("Libsql", "release")]
    [Arguments("Libsql", "release-all")]
    [Arguments("Libsql", "release-304")]
    public async Task InFlightKeyReadRejectsRecreatedEnvironment(string storage, string kind)
    {
        await using var fixture = await StorageFixture.Create(storage);
        var services = fixture.Services;
        var projects = services.GetRequiredService<IProjectRepository>();
        var environments = services.GetRequiredService<IEnvironmentRepository>();
        var entries = services.GetRequiredService<IConfigEntryRepository>();
        var releases = services.GetRequiredService<IConfigReleaseRepository>();
        var keys = services.GetRequiredService<IApiKeyRepository>();
        await projects.AddAsync(new Project { Name = "Alpha" });
        async Task Seed(string value)
        {
            await environments.AddAsync(new ProjectEnvironment { Project = "Alpha", Name = "Production", ActiveReleaseVersion = "1.0.0" });
            var entry = Entry(value);
            entry.Scope = KeyScope.All;
            await entries.AddAsync(entry);
            await releases.AddAsync(new ConfigRelease
            {
                Project = "Alpha",
                Environment = "Production",
                Version = "1.0.0",
                Major = 1,
                Entries = [new ConfigReleaseEntry { Project = "Alpha", Environment = "Production", ReleaseVersion = "1.0.0", Key = "flag", Value = value, Scope = KeyScope.All }]
            });
        }
        await Seed("old");
        await keys.AddAsync(new ApiKey { Name = "key", Project = "Alpha", Environment = "Production", KeyHash = "hash", Fingerprint = "hash", Scope = KeyScope.All });
        async Task<string?> Read(IApiKeyRepository repository)
        {
            var keyService = new FixedKeyService();
            return kind switch
            {
                "value" => (await new GetConfigEntryValueQueryHandler(repository, environments, entries, keyService).Handle(new("Production", "flag"), default)).ErrorCode,
                "all" => (await new GetAllConfigValuesQueryHandler(repository, environments, entries, keyService).Handle(new("Production"), default)).ErrorCode,
                "release" => (await new GetReleaseConfigEntryValueQueryHandler(repository, environments, releases, keyService).Handle(new("Production", "flag"), default)).ErrorCode,
                _ => (await new GetAllReleaseConfigValuesQueryHandler(repository, environments, releases, keyService).Handle(new("Production", IfNoneMatch: kind == "release-304" ? "*" : null), default)).ErrorCode
            };
        }
        await Assert.That(await Read(keys)).IsNull();
        var gatedKeys = new PausedKeyRepository(keys, async () =>
        {
            await entries.DeleteAsync("Alpha", "Production", "flag");
            await releases.DeleteByEnvironmentAsync("Alpha", "Production");
            await environments.DeleteAsync("Alpha", "Production");
            await Seed("replacement secret");
        });
        await Assert.That(await Read(gatedKeys)).IsEqualTo(RuntimeConfigErrorCodes.InvalidApiKey);
    }

    private sealed class FixedKeyService : IApiKeyService
    {
        public string? GetCurrentApiKeyHash() => "hash";
    }

    private sealed class PausedKeyRepository(IApiKeyRepository inner, Func<Task> replace) : IApiKeyRepository
    {
        private bool _first = true;
        public async Task<ApiKeyAuthenticationResult?> GetByKeyHashAsync(string hash, CancellationToken ct = default)
        {
            var result = await inner.GetByKeyHashAsync(hash, ct);
            if (_first) { _first = false; await replace(); }
            return result;
        }
        public Task<ApiKey?> GetByIdAsync(long id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<IReadOnlyList<ApiKey>> ListByProjectAsync(string project, CancellationToken ct = default) => inner.ListByProjectAsync(project, ct);
        public Task AddAsync(ApiKey key, CancellationToken ct = default) => inner.AddAsync(key, ct);
        public Task DeleteAsync(long id, CancellationToken ct = default) => inner.DeleteAsync(id, ct);
    }

    private static ConfigEntry Entry(string value) => new()
    {
        Project = "Alpha",
        Environment = "Production",
        Key = "flag",
        Value = value,
        Scope = KeyScope.Backend,
        ContentType = "text",
        Description = "keep"
    };

    private static ParameterShareLink Link(string token) => new()
    {
        Project = "Alpha",
        Environment = "Production",
        Key = "flag",
        Token = token,
        TokenHash = token,
        CreatedBy = "admin",
        CanEdit = true,
        ExpiresAt = DateTime.UtcNow.AddHours(1)
    };

    private sealed class StorageFixture : IAsyncDisposable
    {
        public required ServiceProvider Services { get; init; }
        public required string Path { get; init; }
        public LocalSqldTestServer? Sqld { get; init; }
        public static async Task<StorageFixture> Create(string storage)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nona-boundary-{Guid.NewGuid():N}.db");
            var sqld = storage == "Libsql" ? await LocalSqldTestServer.StartAsync() : null;
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Type"] = storage,
                ["Storage:Sqlite:DataSource"] = path,
                ["Storage:Libsql:DataSource"] = sqld?.Url,
                ["Storage:Libsql:ManagedPrimary:Enabled"] = "false"
            }).Build();
            var services = new ServiceCollection().AddStorageProvider(configuration).BuildServiceProvider();
            if (storage != "InMemory") await new LibsqlDatabaseInitializer(services.GetRequiredService<ILibsqlDatabaseClient>()).StartAsync(default);
            return new StorageFixture { Services = services, Path = path, Sqld = sqld };
        }
        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            if (Sqld is not null) await Sqld.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(Path);
        }
    }
}
