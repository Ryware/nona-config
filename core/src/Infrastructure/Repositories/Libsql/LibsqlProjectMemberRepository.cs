using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using Nona.Libsql;

namespace Nona.Infrastructure.Repositories.Libsql;

public sealed class LibsqlProjectMemberRepository : IProjectMemberRepository
{
    private readonly ILibsqlDatabaseClient _client;

    public LibsqlProjectMemberRepository(ILibsqlDatabaseClient client)
    {
        _client = client;
    }

    public async Task<ProjectMember?> GetAsync(string username, string projectName, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Username, ProjectName AS ProjectId, Role, CreatedAt
            FROM ProjectMembers
            WHERE Username = @Username COLLATE NOCASE
              AND ProjectName = @ProjectName COLLATE NOCASE
            LIMIT 1
            """,
            LibsqlParameters.Create(
                ("Username", username),
                ("ProjectName", projectName)),
            ct);

        return result.Rows.Count == 0 ? null : Map(result.Rows[0]);
    }

    public async Task<IReadOnlyList<ProjectMember>> ListByUserAsync(string username, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Username, ProjectName AS ProjectId, Role, CreatedAt
            FROM ProjectMembers
            WHERE Username = @Username COLLATE NOCASE
            ORDER BY ProjectName
            """,
            LibsqlParameters.Create(("Username", username)),
            ct);

        return result.Rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ProjectMember>> ListByProjectAsync(string projectName, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Username, ProjectName AS ProjectId, Role, CreatedAt
            FROM ProjectMembers
            WHERE ProjectName = @ProjectName COLLATE NOCASE
            ORDER BY Username
            """,
            LibsqlParameters.Create(("ProjectName", projectName)),
            ct);

        return result.Rows.Select(Map).ToList();
    }

    public async Task<bool> ExistsAsync(string username, string projectName, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT COUNT(1)
            FROM ProjectMembers
            WHERE Username = @Username COLLATE NOCASE
              AND ProjectName = @ProjectName COLLATE NOCASE
            """,
            LibsqlParameters.Create(
                ("Username", username),
                ("ProjectName", projectName)),
            ct);

        return result.Rows[0].GetInt32(0) > 0;
    }

    public async Task AddAsync(ProjectMember member, CancellationToken ct = default)
    {
        await _client.ExecuteAsync(
            """
            INSERT INTO ProjectMembers (Username, ProjectName, Role, CreatedAt)
            VALUES (@Username, @ProjectId, @Role, @CreatedAt)
            """,
            ToParameters(member),
            ct);
    }

    public async Task UpdateAsync(ProjectMember member, CancellationToken ct = default)
    {
        await _client.ExecuteAsync(
            """
            UPDATE ProjectMembers
            SET Role = @Role
            WHERE Username = @Username COLLATE NOCASE
              AND ProjectName = @ProjectId COLLATE NOCASE
            """,
            LibsqlParameters.Create(
                ("Username", member.Username),
                ("ProjectId", member.ProjectId),
                ("Role", (int)member.Role)),
            ct);
    }

    public async Task DeleteAsync(string username, string projectName, CancellationToken ct = default)
    {
        await _client.ExecuteAsync(
            """
            DELETE FROM ProjectMembers
            WHERE Username = @Username COLLATE NOCASE
              AND ProjectName = @ProjectName COLLATE NOCASE
            """,
            LibsqlParameters.Create(
                ("Username", username),
                ("ProjectName", projectName)),
            ct);
    }

    public async Task DeleteByUserAsync(string username, CancellationToken ct = default)
    {
        await _client.ExecuteAsync(
            """
            DELETE FROM ProjectMembers
            WHERE Username = @Username COLLATE NOCASE
            """,
            LibsqlParameters.Create(("Username", username)),
            ct);
    }

    public async Task DeleteByProjectAsync(string projectName, CancellationToken ct = default)
    {
        await _client.ExecuteAsync(
            """
            DELETE FROM ProjectMembers
            WHERE ProjectName = @ProjectName COLLATE NOCASE
            """,
            LibsqlParameters.Create(("ProjectName", projectName)),
            ct);
    }

    private static ProjectMember Map(LibsqlRow row)
    {
        return new ProjectMember
        {
            Username = row.GetString("Username"),
            ProjectId = row.GetString("ProjectId"),
            Role = (ProjectRole)row.GetInt32("Role"),
            CreatedAt = DateTime.Parse(row.GetString("CreatedAt"))
        };
    }

    private static IReadOnlyDictionary<string, object?> ToParameters(ProjectMember member)
    {
        return LibsqlParameters.Create(
            ("Username", member.Username),
            ("ProjectId", member.ProjectId),
            ("Role", (int)member.Role),
            ("CreatedAt", member.CreatedAt.ToString("O")));
    }
}
