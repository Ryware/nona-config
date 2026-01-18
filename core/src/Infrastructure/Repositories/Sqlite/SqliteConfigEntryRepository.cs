using Dapper;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Infrastructure.Repositories.Sqlite;

public class SqliteConfigEntryRepository : IConfigEntryRepository
{
    private readonly SqliteDbContext _dbContext;

    public SqliteConfigEntryRepository(SqliteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ConfigEntry?> GetAsync(string projectName, string environmentName, string key, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT Project, Environment, Key, Value, ContentType, Scope, CreatedAt, UpdatedAt
            FROM ConfigEntries
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Environment = @EnvironmentName COLLATE NOCASE
              AND Key = @Key COLLATE NOCASE
            LIMIT 1";

        var result = await connection.QueryFirstOrDefaultAsync<ConfigEntryDto>(
            sql,
            new { ProjectName = projectName, EnvironmentName = environmentName, Key = key });

        return result?.ToEntity();
    }

    public async Task<IReadOnlyList<ConfigEntry>> ListAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT Project, Environment, Key, Value, ContentType, Scope, CreatedAt, UpdatedAt
            FROM ConfigEntries
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Environment = @EnvironmentName COLLATE NOCASE
            ORDER BY Key";

        var results = await connection.QueryAsync<ConfigEntryDto>(
            sql,
            new { ProjectName = projectName, EnvironmentName = environmentName });

        return results.Select(dto => dto.ToEntity()).ToList();
    }

    public async Task<IReadOnlyList<ConfigEntry>> ListByProjectAsync(string projectName, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT Project, Environment, Key, Value, ContentType, Scope, CreatedAt, UpdatedAt
            FROM ConfigEntries
            WHERE Project = @ProjectName COLLATE NOCASE
            ORDER BY Environment, Key";

        var results = await connection.QueryAsync<ConfigEntryDto>(
            sql,
            new { ProjectName = projectName });

        return results.Select(dto => dto.ToEntity()).ToList();
    }

    public async Task<bool> ExistsAsync(string projectName, string environmentName, string key, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT COUNT(1)
            FROM ConfigEntries
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Environment = @EnvironmentName COLLATE NOCASE
              AND Key = @Key COLLATE NOCASE";

        var count = await connection.ExecuteScalarAsync<int>(
            sql,
            new { ProjectName = projectName, EnvironmentName = environmentName, Key = key });

        return count > 0;
    }

    public async Task AddAsync(ConfigEntry entry, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            INSERT INTO ConfigEntries (Project, Environment, Key, Value, ContentType, Scope, CreatedAt, UpdatedAt)
            VALUES (@Project, @Environment, @Key, @Value, @ContentType, @Scope, @CreatedAt, @UpdatedAt)";

        await connection.ExecuteAsync(sql, ConfigEntryDto.FromEntity(entry));
    }

    public async Task UpdateAsync(ConfigEntry entry, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            UPDATE ConfigEntries
            SET Value = @Value,
                ContentType = @ContentType,
                Scope = @Scope,
                UpdatedAt = @UpdatedAt
            WHERE Project = @Project COLLATE NOCASE
              AND Environment = @Environment COLLATE NOCASE
              AND Key = @Key COLLATE NOCASE";

        await connection.ExecuteAsync(sql, ConfigEntryDto.FromEntity(entry));
    }

    public async Task UpsertAsync(ConfigEntry entry, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            INSERT INTO ConfigEntries (Project, Environment, Key, Value, ContentType, Scope, CreatedAt, UpdatedAt)
            VALUES (@Project, @Environment, @Key, @Value, @ContentType, @Scope, @CreatedAt, @UpdatedAt)
            ON CONFLICT(Project, Environment, Key) DO UPDATE SET
                Value = excluded.Value,
                ContentType = excluded.ContentType,
                Scope = excluded.Scope,
                UpdatedAt = excluded.UpdatedAt";

        await connection.ExecuteAsync(sql, ConfigEntryDto.FromEntity(entry));
    }

    public async Task DeleteAsync(string projectName, string environmentName, string key, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            DELETE FROM ConfigEntries
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Environment = @EnvironmentName COLLATE NOCASE
              AND Key = @Key COLLATE NOCASE";

        await connection.ExecuteAsync(sql, new { ProjectName = projectName, EnvironmentName = environmentName, Key = key });
    }

    public async Task DeleteManyAsync(string projectName, string environmentName, IEnumerable<string> keys, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            DELETE FROM ConfigEntries
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Environment = @EnvironmentName COLLATE NOCASE
              AND Key = @Key COLLATE NOCASE";

        foreach (var key in keys)
        {
            await connection.ExecuteAsync(sql, new { ProjectName = projectName, EnvironmentName = environmentName, Key = key });
        }
    }

    // DTO for database mapping
    private class ConfigEntryDto
    {
        public string Project { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string ContentType { get; set; } = "string";
        public int Scope { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;

        public ConfigEntry ToEntity()
        {
            return new ConfigEntry
            {
                Project = Project,
                Environment = Environment,
                Key = Key,
                Value = Value,
                ContentType = ContentType,
                Scope = (KeyScope)Scope,
                CreatedAt = DateTime.Parse(CreatedAt),
                UpdatedAt = DateTime.Parse(UpdatedAt)
            };
        }

        public static ConfigEntryDto FromEntity(ConfigEntry entry)
        {
            return new ConfigEntryDto
            {
                Project = entry.Project,
                Environment = entry.Environment,
                Key = entry.Key,
                Value = entry.Value,
                ContentType = entry.ContentType,
                Scope = (int)entry.Scope,
                CreatedAt = entry.CreatedAt.ToString("O"),
                UpdatedAt = entry.UpdatedAt.ToString("O")
            };
        }
    }
}
