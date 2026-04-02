using Dapper;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Infrastructure.Repositories.Sqlite;

public class SqliteProjectMemberRepository : IProjectMemberRepository
{
    private readonly SqliteDbContext _dbContext;

    public SqliteProjectMemberRepository(SqliteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProjectMember?> GetAsync(string username, string projectName, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT Username, ProjectName, Role, CreatedAt
            FROM ProjectMembers
            WHERE Username = @Username COLLATE NOCASE
              AND ProjectName = @ProjectName COLLATE NOCASE
            LIMIT 1";

        var result = await connection.QueryFirstOrDefaultAsync<ProjectMemberDto>(
            sql,
            new { Username = username, ProjectName = projectName });

        return result?.ToEntity();
    }

    public async Task<IReadOnlyList<ProjectMember>> ListByUserAsync(string username, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT Username, ProjectName, Role, CreatedAt
            FROM ProjectMembers
            WHERE Username = @Username COLLATE NOCASE
            ORDER BY ProjectName";

        var results = await connection.QueryAsync<ProjectMemberDto>(sql, new { Username = username });

        return results.Select(dto => dto.ToEntity()).ToList();
    }

    public async Task<IReadOnlyList<ProjectMember>> ListByProjectAsync(string projectName, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT Username, ProjectName, Role, CreatedAt
            FROM ProjectMembers
            WHERE ProjectName = @ProjectName COLLATE NOCASE
            ORDER BY Username";

        var results = await connection.QueryAsync<ProjectMemberDto>(sql, new { ProjectName = projectName });

        return results.Select(dto => dto.ToEntity()).ToList();
    }

    public async Task<bool> ExistsAsync(string username, string projectName, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT COUNT(1)
            FROM ProjectMembers
            WHERE Username = @Username COLLATE NOCASE
              AND ProjectName = @ProjectName COLLATE NOCASE";

        var count = await connection.ExecuteScalarAsync<int>(
            sql,
            new { Username = username, ProjectName = projectName });

        return count > 0;
    }

    public async Task AddAsync(ProjectMember member, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            INSERT INTO ProjectMembers (Username, ProjectName, Role, CreatedAt)
            VALUES (@Username, @ProjectName, @Role, @CreatedAt)";

        await connection.ExecuteAsync(sql, ProjectMemberDto.FromEntity(member));
    }

    public async Task UpdateAsync(ProjectMember member, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            UPDATE ProjectMembers
            SET Role = @Role
            WHERE Username = @Username COLLATE NOCASE
              AND ProjectName = @ProjectName COLLATE NOCASE";

        await connection.ExecuteAsync(sql, ProjectMemberDto.FromEntity(member));
    }

    public async Task DeleteAsync(string username, string projectName, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            DELETE FROM ProjectMembers
            WHERE Username = @Username COLLATE NOCASE
              AND ProjectName = @ProjectName COLLATE NOCASE";

        await connection.ExecuteAsync(sql, new { Username = username, ProjectName = projectName });
    }

    public async Task DeleteByUserAsync(string username, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            DELETE FROM ProjectMembers
            WHERE Username = @Username COLLATE NOCASE";

        await connection.ExecuteAsync(sql, new { Username = username });
    }

    public async Task DeleteByProjectAsync(string projectName, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            DELETE FROM ProjectMembers
            WHERE ProjectName = @ProjectName COLLATE NOCASE";

        await connection.ExecuteAsync(sql, new { ProjectName = projectName });
    }

    private class ProjectMemberDto
    {
        public string Username { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public int Role { get; set; }
        public string CreatedAt { get; set; } = string.Empty;

        public ProjectMember ToEntity()
        {
            return new ProjectMember
            {
                Username = Username,
                ProjectId = ProjectId,
                Role = (ProjectRole)Role,
                CreatedAt = DateTime.Parse(CreatedAt)
            };
        }

        public static ProjectMemberDto FromEntity(ProjectMember member)
        {
            return new ProjectMemberDto
            {
                Username = member.Username,
                ProjectId = member.ProjectId,
                Role = (int)member.Role,
                CreatedAt = member.CreatedAt.ToString("O")
            };
        }
    }
}
