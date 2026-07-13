using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using Nona.Libsql;

namespace Nona.Infrastructure.Repositories.Libsql;

public sealed class LibsqlEnvironmentRepository : IEnvironmentRepository
{
    private readonly ILibsqlDatabaseClient _client;

    public LibsqlEnvironmentRepository(ILibsqlDatabaseClient client)
    {
        _client = client;
    }

    public async Task<ProjectEnvironment?> GetAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Name, Project, CreatedAt, UpdatedAt
            FROM Environments
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Name = @EnvironmentName COLLATE NOCASE
            LIMIT 1
            """,
            LibsqlParameters.Create(
                ("ProjectName", projectName),
                ("EnvironmentName", environmentName)),
            ct);

        return result.Rows.Count == 0 ? null : Map(result.Rows[0]);
    }

    public async Task<IReadOnlyList<ProjectEnvironment>> ListByProjectAsync(string projectName, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Name, Project, CreatedAt, UpdatedAt
            FROM Environments
            WHERE Project = @ProjectName COLLATE NOCASE
            ORDER BY Name
            """,
            LibsqlParameters.Create(("ProjectName", projectName)),
            ct);

        return result.Rows.Select(Map).ToList();
    }

    public async Task<bool> ExistsAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT COUNT(1)
            FROM Environments
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Name = @EnvironmentName COLLATE NOCASE
            """,
            LibsqlParameters.Create(
                ("ProjectName", projectName),
                ("EnvironmentName", environmentName)),
            ct);

        return result.Rows[0].GetInt32(0) > 0;
    }

    public async Task AddAsync(ProjectEnvironment environment, CancellationToken ct = default)
    {
        await _client.ExecuteAsync(
            """
            INSERT INTO Environments (Name, Project, CreatedAt, UpdatedAt)
            VALUES (@Name, @Project, @CreatedAt, @UpdatedAt)
            """,
            ToParameters(environment),
            ct);
    }

    public async Task UpdateAsync(ProjectEnvironment environment, CancellationToken ct = default)
    {
        await _client.ExecuteAsync(
            """
            UPDATE Environments
            SET UpdatedAt = @UpdatedAt
            WHERE Project = @Project COLLATE NOCASE
              AND Name = @Name COLLATE NOCASE
            """,
            LibsqlParameters.Create(
                ("Name", environment.Name),
                ("Project", environment.Project),
                ("UpdatedAt", environment.UpdatedAt.ToString("O"))),
            ct);
    }

    public async Task DeleteAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        await _client.ExecuteAsync(
            """
            DELETE FROM Environments
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Name = @EnvironmentName COLLATE NOCASE
            """,
            LibsqlParameters.Create(
                ("ProjectName", projectName),
                ("EnvironmentName", environmentName)),
            ct);
    }

    private static ProjectEnvironment Map(LibsqlRow row)
    {
        return new ProjectEnvironment
        {
            Name = row.GetString("Name"),
            Project = row.GetString("Project"),
            CreatedAt = DateTime.Parse(row.GetString("CreatedAt")),
            UpdatedAt = DateTime.Parse(row.GetString("UpdatedAt"))
        };
    }

    private static IReadOnlyDictionary<string, object?> ToParameters(ProjectEnvironment environment)
    {
        return LibsqlParameters.Create(
            ("Name", environment.Name),
            ("Project", environment.Project),
            ("CreatedAt", environment.CreatedAt.ToString("O")),
            ("UpdatedAt", environment.UpdatedAt.ToString("O")));
    }
}
