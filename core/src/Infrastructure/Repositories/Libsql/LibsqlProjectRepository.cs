using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using Nona.Libsql;

namespace Nona.Infrastructure.Repositories.Libsql;

public sealed class LibsqlProjectRepository : IProjectRepository
{
    private readonly ILibsqlDatabaseClient _client;

    public LibsqlProjectRepository(ILibsqlDatabaseClient client)
    {
        _client = client;
    }

    public async Task<Project?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT rowid AS Id, Name, UrlSlug, CreatedAt, UpdatedAt
            FROM Projects
            WHERE Name = @Name COLLATE NOCASE
            LIMIT 1
            """,
            LibsqlParameters.Create(("Name", name)),
            ct);

        return result.Rows.Count == 0 ? null : Map(result.Rows[0]);
    }

    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT rowid AS Id, Name, UrlSlug, CreatedAt, UpdatedAt
            FROM Projects
            ORDER BY Name
            """,
            ct: ct);

        return result.Rows.Select(Map).ToList();
    }

    public async Task<bool> ExistsAsync(string name, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT COUNT(1)
            FROM Projects
            WHERE Name = @Name COLLATE NOCASE
            """,
            LibsqlParameters.Create(("Name", name)),
            ct);

        return result.Rows[0].GetInt32(0) > 0;
    }

    public async Task AddAsync(Project project, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            INSERT INTO Projects (Name, UrlSlug, CreatedAt, UpdatedAt)
            VALUES (@Name, @UrlSlug, @CreatedAt, @UpdatedAt)
            RETURNING rowid AS Id
            """,
            ToParameters(project),
            ct);

        project.Id = result.Rows.Count > 0 ? result.Rows[0].GetInt64("Id") : result.LastInsertRowId ?? 0;
    }

    public async Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        await _client.ExecuteAsync(
            """
            UPDATE Projects
            SET Name = @Name,
                UpdatedAt = @UpdatedAt
            WHERE UrlSlug = @UrlSlug COLLATE NOCASE
            """,
            LibsqlParameters.Create(
                ("Name", project.Name),
                ("UrlSlug", project.UrlSlug),
                ("UpdatedAt", project.UpdatedAt.ToString("O"))),
            ct);
    }

    public async Task DeleteAsync(string name, CancellationToken ct = default)
    {
        await _client.ExecuteAsync(
            """
            DELETE FROM Projects
            WHERE Name = @Name COLLATE NOCASE
            """,
            LibsqlParameters.Create(("Name", name)),
            ct);
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync("SELECT COUNT(*) FROM Projects", ct: ct);
        return result.Rows[0].GetInt32(0);
    }

    private static Project Map(LibsqlRow row)
    {
        return new Project
        {
            Id = row.GetInt64("Id"),
            Name = row.GetString("Name"),
            UrlSlug = row.GetNullableString("UrlSlug"),
            CreatedAt = DateTime.Parse(row.GetString("CreatedAt")),
            UpdatedAt = DateTime.Parse(row.GetString("UpdatedAt"))
        };
    }

    private static IReadOnlyDictionary<string, object?> ToParameters(Project project)
    {
        return LibsqlParameters.Create(
            ("Name", project.Name),
            ("UrlSlug", project.UrlSlug),
            ("CreatedAt", project.CreatedAt.ToString("O")),
            ("UpdatedAt", project.UpdatedAt.ToString("O")));
    }
}
