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
            SELECT Name, Project, ActiveReleaseVersion, CreatedAt, UpdatedAt
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
            SELECT Name, Project, ActiveReleaseVersion, CreatedAt, UpdatedAt
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
            INSERT INTO Environments (Name, Project, ActiveReleaseVersion, CreatedAt, UpdatedAt)
            VALUES (@Name, @Project, @ActiveReleaseVersion, @CreatedAt, @UpdatedAt)
            """,
            ToParameters(environment),
            ct);
    }

    public async Task UpdateAsync(ProjectEnvironment environment, CancellationToken ct = default)
    {
        await _client.ExecuteAsync(
            """
            UPDATE Environments
            SET ActiveReleaseVersion = @ActiveReleaseVersion,
                UpdatedAt = @UpdatedAt
            WHERE Project = @Project COLLATE NOCASE
              AND Name = @Name COLLATE NOCASE
            """,
            LibsqlParameters.Create(
                ("Name", environment.Name),
                ("Project", environment.Project),
                ("ActiveReleaseVersion", environment.ActiveReleaseVersion),
                ("UpdatedAt", environment.UpdatedAt.ToString("O"))),
            ct);
    }

    public async Task RenameAsync(
        string projectName,
        string currentName,
        string newName,
        DateTime updatedAt,
        CancellationToken ct = default)
    {
        var parameters = LibsqlParameters.Create(
            ("Project", projectName),
            ("CurrentName", currentName),
            ("NewName", newName),
            ("UpdatedAt", updatedAt.ToString("O")));

        await _client.ExecuteBatchAsync(
        [
            new LibsqlStatement(
                """
                UPDATE Environments
                SET Name = @NewName, UpdatedAt = @UpdatedAt
                WHERE Project = @Project COLLATE NOCASE
                  AND Name = @CurrentName COLLATE NOCASE
                """,
                parameters),
            RenameEnvironmentIn("ConfigEntries", parameters),
            RenameEnvironmentIn("ConfigEntryVersions", parameters),
            RenameEnvironmentIn("ParameterShareLinks", parameters),
            RenameEnvironmentIn("ConfigReleases", parameters),
            RenameEnvironmentIn("ConfigReleaseEntries", parameters),
            RenameEnvironmentIn("ApiKeys", parameters)
        ], ct);
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
            ActiveReleaseVersion = row.GetNullableString("ActiveReleaseVersion"),
            CreatedAt = DateTime.Parse(row.GetString("CreatedAt")),
            UpdatedAt = DateTime.Parse(row.GetString("UpdatedAt"))
        };
    }

    private static IReadOnlyDictionary<string, object?> ToParameters(ProjectEnvironment environment)
    {
        return LibsqlParameters.Create(
            ("Name", environment.Name),
            ("Project", environment.Project),
            ("ActiveReleaseVersion", environment.ActiveReleaseVersion),
            ("CreatedAt", environment.CreatedAt.ToString("O")),
            ("UpdatedAt", environment.UpdatedAt.ToString("O")));
    }

    private static LibsqlStatement RenameEnvironmentIn(
        string table,
        IReadOnlyDictionary<string, object?> parameters)
    {
        return new LibsqlStatement(
            $"""
            UPDATE {table}
            SET Environment = @NewName
            WHERE Project = @Project COLLATE NOCASE
              AND Environment = @CurrentName COLLATE NOCASE
            """,
            parameters);
    }
}
