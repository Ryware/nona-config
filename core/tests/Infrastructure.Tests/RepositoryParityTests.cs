using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using Nona.Infrastructure.Tests.Common;

namespace Nona.Infrastructure.Tests;

public class RepositoryParityTests
{
    [Test]
    public async Task ProjectRepositories_ReturnIdenticalResults()
    {
        await using var sqlite = await SqliteRepositorySet.CreateAsync();
        await using var libsql = await LibsqlRepositorySet.CreateAsync();

        var sqliteSnapshot = await ExerciseProjectRepositoryAsync(sqlite.Projects);
        var libsqlSnapshot = await ExerciseProjectRepositoryAsync(libsql.Projects);

        await SnapshotAssert.EqualAsync(sqliteSnapshot, libsqlSnapshot);
    }

    [Test]
    public async Task UserRepositories_ReturnIdenticalResults()
    {
        await using var sqlite = await SqliteRepositorySet.CreateAsync();
        await using var libsql = await LibsqlRepositorySet.CreateAsync();

        var sqliteSnapshot = await ExerciseUserRepositoryAsync(sqlite.Users);
        var libsqlSnapshot = await ExerciseUserRepositoryAsync(libsql.Users);

        await SnapshotAssert.EqualAsync(sqliteSnapshot, libsqlSnapshot);
    }

    [Test]
    public async Task EnvironmentRepositories_ReturnIdenticalResults()
    {
        await using var sqlite = await SqliteRepositorySet.CreateAsync();
        await using var libsql = await LibsqlRepositorySet.CreateAsync();

        var sqliteSnapshot = await ExerciseEnvironmentRepositoryAsync(sqlite.Environments);
        var libsqlSnapshot = await ExerciseEnvironmentRepositoryAsync(libsql.Environments);

        await SnapshotAssert.EqualAsync(sqliteSnapshot, libsqlSnapshot);
    }

    [Test]
    public async Task ConfigEntryRepositories_ReturnIdenticalResults()
    {
        await using var sqlite = await SqliteRepositorySet.CreateAsync();
        await using var libsql = await LibsqlRepositorySet.CreateAsync();

        var sqliteSnapshot = await ExerciseConfigEntryRepositoryAsync(sqlite.ConfigEntries);
        var libsqlSnapshot = await ExerciseConfigEntryRepositoryAsync(libsql.ConfigEntries);

        await SnapshotAssert.EqualAsync(sqliteSnapshot, libsqlSnapshot);
    }

    [Test]
    public async Task ProjectMemberRepositories_ReturnIdenticalResults()
    {
        await using var sqlite = await SqliteRepositorySet.CreateAsync();
        await using var libsql = await LibsqlRepositorySet.CreateAsync();

        var sqliteSnapshot = await ExerciseProjectMemberRepositoryAsync(sqlite.ProjectMembers);
        var libsqlSnapshot = await ExerciseProjectMemberRepositoryAsync(libsql.ProjectMembers);

        await SnapshotAssert.EqualAsync(sqliteSnapshot, libsqlSnapshot);
    }

    [Test]
    public async Task ConfigEntryRepositories_HandleLargeQueriesIdentically()
    {
        await using var sqlite = await SqliteRepositorySet.CreateAsync();
        await using var libsql = await LibsqlRepositorySet.CreateAsync();

        var sqliteSnapshot = await ExerciseLargeConfigEntryQueryAsync(sqlite.ConfigEntries);
        var libsqlSnapshot = await ExerciseLargeConfigEntryQueryAsync(libsql.ConfigEntries);

        await SnapshotAssert.EqualAsync(sqliteSnapshot, libsqlSnapshot);
    }

    private static async Task<ProjectRepositorySnapshot> ExerciseProjectRepositoryAsync(IProjectRepository repository)
    {
        var createdAt = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2025, 1, 11, 13, 30, 0, DateTimeKind.Utc);

        var emptyList = await repository.ListAsync();
        var missingProject = await repository.GetByNameAsync("missing");
        var missingApiKey = await repository.GetByApiKeyAsync("missing");

        var project = new Project
        {
            Name = "alpha",
            UrlSlug = "alpha",
            ServerApiKey = "server-1",
            ClientApiKey = "client-1",
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        await repository.AddAsync(project);

        project.ServerApiKey = "server-2";
        project.ClientApiKey = "client-2";
        project.UpdatedAt = updatedAt;
        await repository.UpdateAsync(project);

        var byName = await repository.GetByNameAsync("ALPHA");
        var serverLookup = await repository.GetByApiKeyAsync("server-2");
        var clientLookup = await repository.GetByApiKeyAsync("client-2");
        var list = await repository.ListAsync();
        var exists = await repository.ExistsAsync("ALPHA");
        var countBeforeDelete = await repository.CountAsync();

        await repository.DeleteAsync("alpha");

        return new ProjectRepositorySnapshot(
            emptyList.Count,
            missingProject is null,
            missingApiKey is null,
            project.Id,
            Normalize(byName),
            Normalize(serverLookup),
            Normalize(clientLookup),
            list.Select(Normalize).ToList(),
            exists,
            countBeforeDelete,
            await repository.CountAsync(),
            await repository.GetByNameAsync("alpha") is null);
    }

    private static async Task<UserRepositorySnapshot> ExerciseUserRepositoryAsync(IUserRepository repository)
    {
        var createdAt = new DateTime(2025, 2, 1, 9, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2025, 2, 2, 10, 0, 0, DateTimeKind.Utc);

        var emptyList = await repository.ListAsync();
        var existsAnyBefore = await repository.ExistsAnyAsync();

        var user = new User
        {
            Email = "user@example.com",
            Name = "User One",
            PasswordHash = "hash-1",
            PasswordSalt = "salt-1",
            Role = UserRole.Viewer,
            Scope = KeyScope.Frontend,
            IsAdmin = false,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            PasswordResetToken = null
        };

        await repository.AddAsync(user);

        user.Name = "User Two";
        user.PasswordHash = "hash-2";
        user.PasswordSalt = "salt-2";
        user.Role = UserRole.Editor;
        user.Scope = KeyScope.Backend;
        user.IsAdmin = true;
        user.PasswordResetToken = "reset-token";
        user.UpdatedAt = updatedAt;
        await repository.UpdateAsync(user);

        var byEmail = await repository.GetAsync("USER@example.com");
        var byId = await repository.GetByIdAsync(user.Id);
        var list = await repository.ListAsync();
        var exists = await repository.ExistsAsync("USER@example.com");
        var existsAnyAfterAdd = await repository.ExistsAnyAsync();
        var countBeforeDelete = await repository.CountAsync();
        var deleted = await repository.DeleteAsync("USER@example.com");
        var deleteMissing = await repository.DeleteAsync("user@example.com");

        return new UserRepositorySnapshot(
            emptyList.Count,
            existsAnyBefore,
            user.Id,
            Normalize(byEmail),
            Normalize(byId),
            list.Select(Normalize).ToList(),
            exists,
            existsAnyAfterAdd,
            countBeforeDelete,
            deleted,
            deleteMissing,
            await repository.ExistsAnyAsync(),
            await repository.CountAsync());
    }

    private static async Task<EnvironmentRepositorySnapshot> ExerciseEnvironmentRepositoryAsync(IEnvironmentRepository repository)
    {
        var createdAt = new DateTime(2025, 3, 5, 8, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2025, 3, 6, 9, 0, 0, DateTimeKind.Utc);

        var emptyList = await repository.ListByProjectAsync("alpha");
        var missing = await repository.GetAsync("alpha", "prod");

        var development = new ProjectEnvironment
        {
            Name = "development",
            Project = "alpha",
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        var production = new ProjectEnvironment
        {
            Name = "production",
            Project = "alpha",
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        await repository.AddAsync(production);
        await repository.AddAsync(development);

        var developmentUpdate = new ProjectEnvironment
        {
            Name = "development",
            Project = "alpha",
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        await repository.UpdateAsync(developmentUpdate);

        var byName = await repository.GetAsync("ALPHA", "DEVELOPMENT");
        var list = await repository.ListByProjectAsync("alpha");
        var exists = await repository.ExistsAsync("alpha", "production");

        await repository.DeleteAsync("alpha", "production");

        return new EnvironmentRepositorySnapshot(
            emptyList.Count,
            missing is null,
            Normalize(byName),
            list.Select(Normalize).ToList(),
            exists,
            await repository.ExistsAsync("alpha", "production"),
            (await repository.ListByProjectAsync("alpha")).Select(Normalize).ToList());
    }

    private static async Task<ConfigEntryRepositorySnapshot> ExerciseConfigEntryRepositoryAsync(IConfigEntryRepository repository)
    {
        var createdAt = new DateTime(2025, 4, 1, 11, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2025, 4, 2, 12, 0, 0, DateTimeKind.Utc);

        var emptyList = await repository.ListAsync("alpha", "production");
        var missing = await repository.GetAsync("alpha", "production", "missing");

        var secondEntry = new ConfigEntry
        {
            Project = "alpha",
            Environment = "production",
            Key = "beta",
            Value = "2",
            ContentType = "string",
            Scope = KeyScope.Backend,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        var firstEntry = new ConfigEntry
        {
            Project = "alpha",
            Environment = "production",
            Key = "alpha",
            Value = "1",
            ContentType = "application/json",
            Scope = KeyScope.All,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        await repository.AddAsync(secondEntry);
        await repository.AddAsync(firstEntry);

        var updatedFirstEntry = new ConfigEntry
        {
            Project = "alpha",
            Environment = "production",
            Key = "alpha",
            Value = "{\"enabled\":true}",
            ContentType = "application/json",
            Scope = KeyScope.Frontend,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        await repository.UpdateAsync(updatedFirstEntry);

        var upsertNew = new ConfigEntry
        {
            Project = "alpha",
            Environment = "staging",
            Key = "gamma",
            Value = "3",
            ContentType = "string",
            Scope = KeyScope.Backend,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        await repository.UpsertAsync(upsertNew);

        var upsertExisting = new ConfigEntry
        {
            Project = "alpha",
            Environment = "production",
            Key = "beta",
            Value = "22",
            ContentType = "text/plain",
            Scope = KeyScope.Backend,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        await repository.UpsertAsync(upsertExisting);

        var byKey = await repository.GetAsync("ALPHA", "PRODUCTION", "ALPHA");
        var list = await repository.ListAsync("alpha", "production");
        var byProject = await repository.ListByProjectAsync("alpha");
        var exists = await repository.ExistsAsync("alpha", "production", "beta");
        var countBeforeDelete = await repository.CountAsync();

        await repository.DeleteManyAsync("alpha", "production", ["alpha", "beta"]);
        await repository.DeleteAsync("alpha", "staging", "gamma");

        return new ConfigEntryRepositorySnapshot(
            emptyList.Count,
            missing is null,
            Normalize(byKey),
            list.Select(Normalize).ToList(),
            byProject.Select(Normalize).ToList(),
            exists,
            countBeforeDelete,
            await repository.CountAsync(),
            (await repository.ListByProjectAsync("alpha")).Count);
    }

    private static async Task<ProjectMemberRepositorySnapshot> ExerciseProjectMemberRepositoryAsync(IProjectMemberRepository repository)
    {
        var createdAt = new DateTime(2025, 5, 1, 8, 0, 0, DateTimeKind.Utc);

        var emptyByUser = await repository.ListByUserAsync("user@example.com");
        var emptyByProject = await repository.ListByProjectAsync("alpha");
        var missing = await repository.GetAsync("user@example.com", "alpha");

        var alphaMember = new ProjectMember
        {
            Username = "user@example.com",
            ProjectId = "alpha",
            Role = ProjectRole.Viewer,
            CreatedAt = createdAt
        };

        var betaMember = new ProjectMember
        {
            Username = "user@example.com",
            ProjectId = "beta",
            Role = ProjectRole.Editor,
            CreatedAt = createdAt
        };

        await repository.AddAsync(alphaMember);
        await repository.AddAsync(betaMember);

        var alphaUpdate = new ProjectMember
        {
            Username = "user@example.com",
            ProjectId = "alpha",
            Role = ProjectRole.Editor,
            CreatedAt = createdAt
        };

        await repository.UpdateAsync(alphaUpdate);

        var byCompositeKey = await repository.GetAsync("USER@example.com", "ALPHA");
        var byUser = await repository.ListByUserAsync("user@example.com");
        var byProject = await repository.ListByProjectAsync("beta");
        var exists = await repository.ExistsAsync("USER@example.com", "beta");

        await repository.DeleteAsync("user@example.com", "beta");

        await repository.AddAsync(new ProjectMember
        {
            Username = "user@example.com",
            ProjectId = "gamma",
            Role = ProjectRole.Viewer,
            CreatedAt = createdAt
        });
        await repository.DeleteByUserAsync("user@example.com");

        await repository.AddAsync(new ProjectMember
        {
            Username = "a@example.com",
            ProjectId = "shared",
            Role = ProjectRole.Viewer,
            CreatedAt = createdAt
        });
        await repository.AddAsync(new ProjectMember
        {
            Username = "b@example.com",
            ProjectId = "shared",
            Role = ProjectRole.Editor,
            CreatedAt = createdAt
        });
        await repository.DeleteByProjectAsync("shared");

        return new ProjectMemberRepositorySnapshot(
            emptyByUser.Count,
            emptyByProject.Count,
            missing is null,
            Normalize(byCompositeKey),
            byUser.Select(Normalize).ToList(),
            byProject.Select(Normalize).ToList(),
            exists,
            (await repository.ListByUserAsync("user@example.com")).Count,
            (await repository.ListByProjectAsync("shared")).Count);
    }

    private static async Task<LargeConfigEntrySnapshot> ExerciseLargeConfigEntryQueryAsync(IConfigEntryRepository repository)
    {
        var createdAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        const int count = 150;

        for (var index = 0; index < count; index++)
        {
            await repository.UpsertAsync(new ConfigEntry
            {
                Project = "alpha",
                Environment = "production",
                Key = $"key-{index:D3}",
                Value = $"value-{index:D3}",
                ContentType = "string",
                Scope = index % 2 == 0 ? KeyScope.Backend : KeyScope.Frontend,
                CreatedAt = createdAt,
                UpdatedAt = createdAt.AddMinutes(index)
            });
        }

        var list = await repository.ListAsync("alpha", "production");

        return new LargeConfigEntrySnapshot(
            list.Count,
            list.First().Key,
            list.Last().Key,
            list.Take(5).Select(Normalize).ToList(),
            list.Skip(count - 5).Select(Normalize).ToList());
    }

    private static ProjectShape? Normalize(Project? project)
        => project is null ? null : new ProjectShape(project.Id, project.Name, project.UrlSlug, project.ServerApiKey, project.ClientApiKey, project.CreatedAt, project.UpdatedAt);

    private static ApiKeyLookupShape? Normalize(ApiKeyLookupResult? result)
        => result is null ? null : new ApiKeyLookupShape(Normalize(result.Project)!, result.Scope);

    private static UserShape? Normalize(User? user)
        => user is null ? null : new UserShape(user.Id, user.Email, user.Name, user.PasswordHash, user.PasswordSalt, user.Role, user.Scope, user.IsAdmin, user.CreatedAt, user.UpdatedAt, user.PasswordResetToken);

    private static EnvironmentShape? Normalize(ProjectEnvironment? environment)
        => environment is null ? null : new EnvironmentShape(environment.Name, environment.Project, environment.CreatedAt, environment.UpdatedAt);

    private static ConfigEntryShape? Normalize(ConfigEntry? entry)
        => entry is null ? null : new ConfigEntryShape(entry.Project, entry.Environment, entry.Key, entry.Value, entry.ContentType, entry.Scope, entry.CreatedAt, entry.UpdatedAt);

    private static ProjectMemberShape? Normalize(ProjectMember? member)
        => member is null ? null : new ProjectMemberShape(member.Username, member.ProjectId, member.Role, member.CreatedAt);

    private sealed record ProjectRepositorySnapshot(int EmptyListCount, bool MissingProjectWasNull, bool MissingApiKeyWasNull, long AssignedId, ProjectShape? ByName, ApiKeyLookupShape? ServerLookup, ApiKeyLookupShape? ClientLookup, List<ProjectShape?> ListedProjects, bool Exists, int CountBeforeDelete, int CountAfterDelete, bool MissingAfterDelete);
    private sealed record UserRepositorySnapshot(int EmptyListCount, bool ExistsAnyBefore, long AssignedId, UserShape? ByEmail, UserShape? ById, List<UserShape?> ListedUsers, bool Exists, bool ExistsAnyAfterAdd, int CountBeforeDelete, bool Deleted, bool DeleteMissing, bool ExistsAnyAfterDelete, int CountAfterDelete);
    private sealed record EnvironmentRepositorySnapshot(int EmptyListCount, bool MissingWasNull, EnvironmentShape? DevelopmentEnvironment, List<EnvironmentShape?> ListedEnvironments, bool Exists, bool ExistsAfterDelete, List<EnvironmentShape?> RemainingEnvironments);
    private sealed record ConfigEntryRepositorySnapshot(int EmptyListCount, bool MissingWasNull, ConfigEntryShape? EntryByKey, List<ConfigEntryShape?> ListedEntries, List<ConfigEntryShape?> ListedByProject, bool Exists, int CountBeforeDelete, int CountAfterDelete, int RemainingByProjectCount);
    private sealed record ProjectMemberRepositorySnapshot(int EmptyByUserCount, int EmptyByProjectCount, bool MissingWasNull, ProjectMemberShape? MemberByCompositeKey, List<ProjectMemberShape?> MembersByUser, List<ProjectMemberShape?> MembersByProject, bool Exists, int RemainingByUserCount, int RemainingSharedProjectCount);
    private sealed record LargeConfigEntrySnapshot(int Count, string FirstKey, string LastKey, List<ConfigEntryShape?> FirstFive, List<ConfigEntryShape?> LastFive);
    private sealed record ProjectShape(long Id, string Name, string? UrlSlug, string? ServerApiKey, string? ClientApiKey, DateTime CreatedAt, DateTime UpdatedAt);
    private sealed record ApiKeyLookupShape(ProjectShape Project, KeyScope Scope);
    private sealed record UserShape(long Id, string Email, string Name, string? PasswordHash, string? PasswordSalt, UserRole Role, KeyScope Scope, bool IsAdmin, DateTime CreatedAt, DateTime UpdatedAt, string? PasswordResetToken);
    private sealed record EnvironmentShape(string Name, string Project, DateTime CreatedAt, DateTime UpdatedAt);
    private sealed record ConfigEntryShape(string Project, string Environment, string Key, string Value, string ContentType, KeyScope Scope, DateTime CreatedAt, DateTime UpdatedAt);
    private sealed record ProjectMemberShape(string Username, string ProjectId, ProjectRole Role, DateTime CreatedAt);
}
