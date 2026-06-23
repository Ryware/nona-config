using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using Nona.Libsql;
using System.Globalization;

namespace Nona.Infrastructure.Repositories.Libsql;

public sealed class LibsqlConfigEntryRepository : IConfigEntryRepository
{
    private readonly ILibsqlDatabaseClient _client;

    public LibsqlConfigEntryRepository(ILibsqlDatabaseClient client)
    {
        _client = client;
    }

    public async Task<ConfigEntry?> GetAsync(string projectName, string environmentName, string key, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Project, Environment, Key, Value, ContentType, Scope, ActiveVersion, CreatedAt, UpdatedAt
            FROM ConfigEntries
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Environment = @EnvironmentName COLLATE NOCASE
              AND Key = @Key COLLATE NOCASE
            LIMIT 1
            """,
            CreateKeyParameters(projectName, environmentName, key),
            ct);

        return result.Rows.Count == 0 ? null : Map(result.Rows[0]);
    }

    public async Task<ConfigEntry?> AddVersionAsync(ConfigEntry entry, string actor, CancellationToken ct = default)
    {
        var normalizedActor = string.IsNullOrWhiteSpace(actor) ? "System" : actor;
        var versionParameters = ToVersionParameters(entry, normalizedActor);
        var entryParameters = ToEntryParameters(entry);
        var keyParameters = ToEntryKeyParameters(entry);

        var results = await _client.ExecuteBatchAsync(
            [
                new LibsqlStatement(
                    """
                    INSERT INTO ConfigEntryVersions (Project, Environment, Key, Version, Value, ContentType, Scope, CreatedAt, Actor)
                    VALUES (
                        @Project,
                        @Environment,
                        @Key,
                        (
                            SELECT COALESCE(MAX(Version), 0) + 1
                            FROM ConfigEntryVersions
                            WHERE Project = @Project COLLATE NOCASE
                              AND Environment = @Environment COLLATE NOCASE
                              AND Key = @Key COLLATE NOCASE
                        ),
                        @Value,
                        @ContentType,
                        @Scope,
                        @VersionCreatedAt,
                        @Actor
                    )
                    """,
                    versionParameters),
                new LibsqlStatement(
                    """
                    INSERT INTO ConfigEntries (Project, Environment, Key, Value, ContentType, Scope, ActiveVersion, CreatedAt, UpdatedAt)
                    VALUES (
                        @Project,
                        @Environment,
                        @Key,
                        @Value,
                        @ContentType,
                        @Scope,
                        (
                            SELECT MAX(Version)
                            FROM ConfigEntryVersions
                            WHERE Project = @Project COLLATE NOCASE
                              AND Environment = @Environment COLLATE NOCASE
                              AND Key = @Key COLLATE NOCASE
                        ),
                        @CreatedAt,
                        @UpdatedAt
                    )
                    ON CONFLICT(Project, Environment, Key) DO UPDATE SET
                        Value = excluded.Value,
                        ContentType = excluded.ContentType,
                        Scope = excluded.Scope,
                        ActiveVersion = excluded.ActiveVersion,
                        UpdatedAt = excluded.UpdatedAt
                    """,
                    entryParameters),
                new LibsqlStatement(
                    """
                    SELECT Project, Environment, Key, Value, ContentType, Scope, ActiveVersion, CreatedAt, UpdatedAt
                    FROM ConfigEntries
                    WHERE Project = @Project COLLATE NOCASE
                      AND Environment = @Environment COLLATE NOCASE
                      AND Key = @Key COLLATE NOCASE
                    LIMIT 1
                    """,
                    keyParameters)
            ],
            ct);

        var savedRows = results[^1].Rows;
        return savedRows.Count == 0 ? null : Map(savedRows[0]);
    }

    public async Task<IReadOnlyList<ConfigEntryVersion>> ListVersionsAsync(string projectName, string environmentName, string key, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Project, Environment, Key, Version, Value, ContentType, Scope, CreatedAt, Actor
            FROM ConfigEntryVersions
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Environment = @EnvironmentName COLLATE NOCASE
              AND Key = @Key COLLATE NOCASE
            ORDER BY Version DESC
            """,
            CreateKeyParameters(projectName, environmentName, key),
            ct);

        return result.Rows.Select(MapVersion).ToList();
    }

    public async Task<ConfigEntryVersion?> GetVersionAsync(string projectName, string environmentName, string key, int version, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Project, Environment, Key, Version, Value, ContentType, Scope, CreatedAt, Actor
            FROM ConfigEntryVersions
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Environment = @EnvironmentName COLLATE NOCASE
              AND Key = @Key COLLATE NOCASE
              AND Version = @Version
            LIMIT 1
            """,
            LibsqlParameters.Create(
                ("ProjectName", projectName),
                ("EnvironmentName", environmentName),
                ("Key", key),
                ("Version", version)),
            ct);

        return result.Rows.Count == 0 ? null : MapVersion(result.Rows[0]);
    }

    public async Task<IReadOnlyList<ConfigEntry>> ListAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Project, Environment, Key, Value, ContentType, Scope, ActiveVersion, CreatedAt, UpdatedAt
            FROM ConfigEntries
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Environment = @EnvironmentName COLLATE NOCASE
            ORDER BY Key
            """,
            LibsqlParameters.Create(
                ("ProjectName", projectName),
                ("EnvironmentName", environmentName)),
            ct);

        return result.Rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ConfigEntry>> ListByProjectAsync(string projectName, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Project, Environment, Key, Value, ContentType, Scope, ActiveVersion, CreatedAt, UpdatedAt
            FROM ConfigEntries
            WHERE Project = @ProjectName COLLATE NOCASE
            ORDER BY Environment, Key
            """,
            LibsqlParameters.Create(("ProjectName", projectName)),
            ct);

        return result.Rows.Select(Map).ToList();
    }

    public async Task<bool> ExistsAsync(string projectName, string environmentName, string key, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT COUNT(1)
            FROM ConfigEntries
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Environment = @EnvironmentName COLLATE NOCASE
              AND Key = @Key COLLATE NOCASE
            """,
            CreateKeyParameters(projectName, environmentName, key),
            ct);

        return result.Rows[0].GetInt32(0) > 0;
    }

    public async Task AddAsync(ConfigEntry entry, CancellationToken ct = default)
    {
        await AddVersionAsync(entry, "System", ct);
    }

    public async Task UpdateAsync(ConfigEntry entry, CancellationToken ct = default)
    {
        await AddVersionAsync(entry, "System", ct);
    }

    public async Task UpsertAsync(ConfigEntry entry, CancellationToken ct = default)
    {
        await AddVersionAsync(entry, "System", ct);
    }

    public async Task DeleteAsync(string projectName, string environmentName, string key, CancellationToken ct = default)
    {
        await _client.ExecuteBatchAsync(
            [
                new LibsqlStatement(
                    """
                    DELETE FROM ConfigEntryVersions
                    WHERE Project = @ProjectName COLLATE NOCASE
                      AND Environment = @EnvironmentName COLLATE NOCASE
                      AND Key = @Key COLLATE NOCASE
                    """,
                    CreateKeyParameters(projectName, environmentName, key)),
                new LibsqlStatement(
                    """
                    DELETE FROM ConfigEntries
                    WHERE Project = @ProjectName COLLATE NOCASE
                      AND Environment = @EnvironmentName COLLATE NOCASE
                      AND Key = @Key COLLATE NOCASE
                    """,
                    CreateKeyParameters(projectName, environmentName, key))
            ],
            ct);
    }

    public async Task DeleteManyAsync(string projectName, string environmentName, IEnumerable<string> keys, CancellationToken ct = default)
    {
        foreach (var key in keys)
        {
            await DeleteAsync(projectName, environmentName, key, ct);
        }
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync("SELECT COUNT(*) FROM ConfigEntries", ct: ct);
        return result.Rows[0].GetInt32(0);
    }

    private static ConfigEntry Map(LibsqlRow row)
    {
        return new ConfigEntry
        {
            Project = row.GetString("Project"),
            Environment = row.GetString("Environment"),
            Key = row.GetString("Key"),
            Value = row.GetString("Value"),
            ContentType = row.GetString("ContentType"),
            Scope = (KeyScope)row.GetInt32("Scope"),
            ActiveVersion = row.GetInt32("ActiveVersion"),
            CreatedAt = ParseTimestamp(row.GetString("CreatedAt")),
            UpdatedAt = ParseTimestamp(row.GetString("UpdatedAt"))
        };
    }

    private static ConfigEntryVersion MapVersion(LibsqlRow row)
    {
        return new ConfigEntryVersion
        {
            Project = row.GetString("Project"),
            Environment = row.GetString("Environment"),
            Key = row.GetString("Key"),
            Version = row.GetInt32("Version"),
            Value = row.GetString("Value"),
            ContentType = row.GetString("ContentType"),
            Scope = (KeyScope)row.GetInt32("Scope"),
            CreatedAt = ParseTimestamp(row.GetString("CreatedAt")),
            Actor = row.GetString("Actor")
        };
    }

    private static DateTime ParseTimestamp(string value)
        => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static IReadOnlyDictionary<string, object?> CreateKeyParameters(
        string projectName,
        string environmentName,
        string key)
    {
        return LibsqlParameters.Create(
            ("ProjectName", projectName),
            ("EnvironmentName", environmentName),
            ("Key", key));
    }

    private static IReadOnlyDictionary<string, object?> ToVersionParameters(ConfigEntry entry, string actor)
    {
        return LibsqlParameters.Create(
            ("Project", entry.Project),
            ("Environment", entry.Environment),
            ("Key", entry.Key),
            ("Value", entry.Value),
            ("ContentType", entry.ContentType),
            ("Scope", (int)entry.Scope),
            ("Actor", actor),
            ("VersionCreatedAt", entry.UpdatedAt.ToString("O")));
    }

    private static IReadOnlyDictionary<string, object?> ToEntryParameters(ConfigEntry entry)
    {
        return LibsqlParameters.Create(
            ("Project", entry.Project),
            ("Environment", entry.Environment),
            ("Key", entry.Key),
            ("Value", entry.Value),
            ("ContentType", entry.ContentType),
            ("Scope", (int)entry.Scope),
            ("CreatedAt", entry.CreatedAt.ToString("O")),
            ("UpdatedAt", entry.UpdatedAt.ToString("O")));
    }

    private static IReadOnlyDictionary<string, object?> ToEntryKeyParameters(ConfigEntry entry)
    {
        return LibsqlParameters.Create(
            ("Project", entry.Project),
            ("Environment", entry.Environment),
            ("Key", entry.Key));
    }
}
