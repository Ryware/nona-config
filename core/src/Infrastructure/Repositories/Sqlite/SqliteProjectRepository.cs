using Dapper;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Infrastructure.Repositories.Sqlite;

public class SqliteProjectRepository : IProjectRepository
{
    private readonly SqliteDbContext _dbContext;

    public SqliteProjectRepository(SqliteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Project?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT Name, ServerApiKey, ClientApiKey, CreatedAt, UpdatedAt
            FROM Projects
            WHERE Name = @Name COLLATE NOCASE
            LIMIT 1";

        var result = await connection.QueryFirstOrDefaultAsync<ProjectDto>(sql, new { Name = name });

        return result?.ToEntity();
    }

    public async Task<ApiKeyLookupResult?> GetByApiKeyAsync(string apiKey, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT Name, ServerApiKey, ClientApiKey, CreatedAt, UpdatedAt
            FROM Projects
            WHERE ServerApiKey = @ApiKey OR ClientApiKey = @ApiKey
            LIMIT 1";

        var result = await connection.QueryFirstOrDefaultAsync<ProjectDto>(sql, new { ApiKey = apiKey });

        if (result == null)
            return null;

        var project = result.ToEntity();

        if (project.ServerApiKey == apiKey)
            return new ApiKeyLookupResult(project, KeyScope.Backend);

        if (project.ClientApiKey == apiKey)
            return new ApiKeyLookupResult(project, KeyScope.Frontend);

        return null;
    }

    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT Name, ServerApiKey, ClientApiKey, CreatedAt, UpdatedAt
            FROM Projects
            ORDER BY Name";

        var results = await connection.QueryAsync<ProjectDto>(sql);

        return results.Select(dto => dto.ToEntity()).ToList();
    }

    public async Task<bool> ExistsAsync(string name, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT COUNT(1)
            FROM Projects
            WHERE Name = @Name COLLATE NOCASE";

        var count = await connection.ExecuteScalarAsync<int>(sql, new { Name = name });

        return count > 0;
    }

    public async Task AddAsync(Project project, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            INSERT INTO Projects (Name, ServerApiKey, ClientApiKey, CreatedAt, UpdatedAt)
            VALUES (@Name, @ServerApiKey, @ClientApiKey, @CreatedAt, @UpdatedAt)";

        await connection.ExecuteAsync(sql, ProjectDto.FromEntity(project));
    }

    public async Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            UPDATE Projects
            SET ServerApiKey = @ServerApiKey,
                ClientApiKey = @ClientApiKey,
                UpdatedAt = @UpdatedAt
            WHERE Name = @Name COLLATE NOCASE";

        await connection.ExecuteAsync(sql, ProjectDto.FromEntity(project));
    }

    public async Task DeleteAsync(string name, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            DELETE FROM Projects
            WHERE Name = @Name COLLATE NOCASE";

        await connection.ExecuteAsync(sql, new { Name = name });
    }

    private class ProjectDto
    {
        public string Name { get; set; } = string.Empty;
        public string? ServerApiKey { get; set; }
        public string? ClientApiKey { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;

        public Project ToEntity()
        {
            return new Project
            {
                Name = Name,
                ServerApiKey = ServerApiKey,
                ClientApiKey = ClientApiKey,
                CreatedAt = DateTime.Parse(CreatedAt),
                UpdatedAt = DateTime.Parse(UpdatedAt)
            };
        }

        public static ProjectDto FromEntity(Project project)
        {
            return new ProjectDto
            {
                Name = project.Name,
                ServerApiKey = project.ServerApiKey,
                ClientApiKey = project.ClientApiKey,
                CreatedAt = project.CreatedAt.ToString("O"),
                UpdatedAt = project.UpdatedAt.ToString("O")
            };
        }
    }
}
