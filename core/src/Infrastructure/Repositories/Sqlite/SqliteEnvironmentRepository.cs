using Dapper;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Infrastructure.Repositories.Sqlite;

public class SqliteEnvironmentRepository : IEnvironmentRepository
{
    private readonly SqliteDbContext _dbContext;

    public SqliteEnvironmentRepository(SqliteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProjectEnvironment?> GetAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT Name, Project, CreatedAt, UpdatedAt
            FROM Environments
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Name = @EnvironmentName COLLATE NOCASE
            LIMIT 1";

        var result = await connection.QueryFirstOrDefaultAsync<EnvironmentDto>(
            sql,
            new { ProjectName = projectName, EnvironmentName = environmentName });

        return result?.ToEntity();
    }

    public async Task<IReadOnlyList<ProjectEnvironment>> ListByProjectAsync(string projectName, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT Name, Project, CreatedAt, UpdatedAt
            FROM Environments
            WHERE Project = @ProjectName COLLATE NOCASE
            ORDER BY Name";

        var results = await connection.QueryAsync<EnvironmentDto>(sql, new { ProjectName = projectName });

        return results.Select(dto => dto.ToEntity()).ToList();
    }

    public async Task<bool> ExistsAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT COUNT(1)
            FROM Environments
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Name = @EnvironmentName COLLATE NOCASE";

        var count = await connection.ExecuteScalarAsync<int>(
            sql,
            new { ProjectName = projectName, EnvironmentName = environmentName });

        return count > 0;
    }

    public async Task AddAsync(ProjectEnvironment environment, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            INSERT INTO Environments (Name, Project, CreatedAt, UpdatedAt)
            VALUES (@Name, @Project, @CreatedAt, @UpdatedAt)";

        await connection.ExecuteAsync(sql, EnvironmentDto.FromEntity(environment));
    }

    public async Task UpdateAsync(ProjectEnvironment environment, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            UPDATE Environments
            SET UpdatedAt = @UpdatedAt
            WHERE Project = @Project COLLATE NOCASE
              AND Name = @Name COLLATE NOCASE";

        await connection.ExecuteAsync(sql, EnvironmentDto.FromEntity(environment));
    }

    public async Task DeleteAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            DELETE FROM Environments
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Name = @EnvironmentName COLLATE NOCASE";

        await connection.ExecuteAsync(sql, new { ProjectName = projectName, EnvironmentName = environmentName });
    }

    private class EnvironmentDto
    {
        public string Name { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;

        public ProjectEnvironment ToEntity()
        {
            return new ProjectEnvironment
            {
                Name = Name,
                Project = Project,
                CreatedAt = DateTime.Parse(CreatedAt),
                UpdatedAt = DateTime.Parse(UpdatedAt)
            };
        }

        public static EnvironmentDto FromEntity(ProjectEnvironment environment)
        {
            return new EnvironmentDto
            {
                Name = environment.Name,
                Project = environment.Project,
                CreatedAt = environment.CreatedAt.ToString("O"),
                UpdatedAt = environment.UpdatedAt.ToString("O")
            };
        }
    }
}
