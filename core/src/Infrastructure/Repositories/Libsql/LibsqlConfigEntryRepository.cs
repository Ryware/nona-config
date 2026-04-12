using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using Nona.Libsql;

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
            SELECT Project, Environment, Key, Value, ContentType, Scope, CreatedAt, UpdatedAt
            FROM ConfigEntries
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Environment = @EnvironmentName COLLATE NOCASE
              AND Key = @Key COLLATE NOCASE
            LIMIT 1
            """,
            new { ProjectName = projectName, EnvironmentName = environmentName, Key = key },
            ct);

        return result.Rows.Count == 0 ? null : Map(result.Rows[0]);
    }

    public async Task<IReadOnlyList<ConfigEntry>> ListAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Project, Environment, Key, Value, ContentType, Scope, CreatedAt, UpdatedAt
            FROM ConfigEntries
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Environment = @EnvironmentName COLLATE NOCASE
            ORDER BY Key
            """,
            new { ProjectName = projectName, EnvironmentName = environmentName },
            ct);

        return result.Rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ConfigEntry>> ListByProjectAsync(string projectName, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Project, Environment, Key, Value, ContentType, Scope, CreatedAt, UpdatedAt
            FROM ConfigEntries
            WHERE Project = @ProjectName COLLATE NOCASE
            ORDER BY Environment, Key
            """,
            new { ProjectName = projectName },
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
            new { ProjectName = projectName, EnvironmentName = environmentName, Key = key },
            ct);

        return result.Rows[0].GetInt32(0) > 0;
    }

    public async Task AddAsync(ConfigEntry entry, CancellationToken ct = default)
    {
        await _client.ExecuteAsync(
            """
            INSERT INTO ConfigEntries (Project, Environment, Key, Value, ContentType, Scope, CreatedAt, UpdatedAt)
            VALUES (@Project, @Environment, @Key, @Value, @ContentType, @Scope, @CreatedAt, @UpdatedAt)
            """,
            ToParameters(entry),
            ct);
    }

    public async Task UpdateAsync(ConfigEntry entry, CancellationToken ct = default)
    {
        await _client.ExecuteAsync(
            """
            UPDATE ConfigEntries
            SET Value = @Value,
                ContentType = @ContentType,
                Scope = @Scope,
                UpdatedAt = @UpdatedAt
            WHERE Project = @Project COLLATE NOCASE
              AND Environment = @Environment COLLATE NOCASE
              AND Key = @Key COLLATE NOCASE
            """,
            ToParameters(entry),
            ct);
    }

    public async Task UpsertAsync(ConfigEntry entry, CancellationToken ct = default)
    {
        await _client.ExecuteAsync(
            """
            INSERT INTO ConfigEntries (Project, Environment, Key, Value, ContentType, Scope, CreatedAt, UpdatedAt)
            VALUES (@Project, @Environment, @Key, @Value, @ContentType, @Scope, @CreatedAt, @UpdatedAt)
            ON CONFLICT(Project, Environment, Key) DO UPDATE SET
                Value = excluded.Value,
                ContentType = excluded.ContentType,
                Scope = excluded.Scope,
                UpdatedAt = excluded.UpdatedAt
            """,
            ToParameters(entry),
            ct);
    }

    public async Task DeleteAsync(string projectName, string environmentName, string key, CancellationToken ct = default)
    {
        await _client.ExecuteAsync(
            """
            DELETE FROM ConfigEntries
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Environment = @EnvironmentName COLLATE NOCASE
              AND Key = @Key COLLATE NOCASE
            """,
            new { ProjectName = projectName, EnvironmentName = environmentName, Key = key },
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
            CreatedAt = DateTime.Parse(row.GetString("CreatedAt")),
            UpdatedAt = DateTime.Parse(row.GetString("UpdatedAt"))
        };
    }

    private static object ToParameters(ConfigEntry entry)
    {
        return new
        {
            entry.Project,
            entry.Environment,
            entry.Key,
            entry.Value,
            entry.ContentType,
            Scope = (int)entry.Scope,
            CreatedAt = entry.CreatedAt.ToString("O"),
            UpdatedAt = entry.UpdatedAt.ToString("O")
        };
    }
}
