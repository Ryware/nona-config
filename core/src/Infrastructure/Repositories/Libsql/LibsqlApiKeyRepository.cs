using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using Nona.Libsql;

namespace Nona.Infrastructure.Repositories.Libsql;

public sealed class LibsqlApiKeyRepository : IApiKeyRepository
{
    private readonly ILibsqlDatabaseClient _client;

    public LibsqlApiKeyRepository(ILibsqlDatabaseClient client)
    {
        _client = client;
    }

    public async Task<ApiKey?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Id, Name, Key, Project, Environment, Scope, CreatedAt, UpdatedAt
            FROM ApiKeys
            WHERE Id = @Id
            LIMIT 1
            """,
            new { Id = id },
            ct);

        return result.Rows.Count == 0 ? null : MapApiKey(result.Rows[0]);
    }

    public async Task<ApiKeyAuthenticationResult?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT
                ak.Scope AS ApiKeyScope,
                ak.Environment AS ApiKeyEnvironment,
                p.rowid AS ProjectId,
                p.Name AS ProjectName,
                p.UrlSlug,
                p.CreatedAt AS ProjectCreatedAt,
                p.UpdatedAt AS ProjectUpdatedAt
            FROM ApiKeys ak
            INNER JOIN Projects p ON p.Name = ak.Project COLLATE NOCASE
            WHERE ak.Key = @Key
            LIMIT 1
            """,
            new { Key = key },
            ct);

        if (result.Rows.Count == 0)
            return null;

        var row = result.Rows[0];
        var project = new Project
        {
            Id = row.GetInt64("ProjectId"),
            Name = row.GetString("ProjectName"),
            UrlSlug = row.GetNullableString("UrlSlug"),
            CreatedAt = DateTime.Parse(row.GetString("ProjectCreatedAt")),
            UpdatedAt = DateTime.Parse(row.GetString("ProjectUpdatedAt"))
        };

        return new ApiKeyAuthenticationResult(
            project,
            (KeyScope)row.GetInt32("ApiKeyScope"),
            row.GetNullableString("ApiKeyEnvironment"));
    }

    public async Task<IReadOnlyList<ApiKey>> ListByProjectAsync(string projectName, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Id, Name, Key, Project, Environment, Scope, CreatedAt, UpdatedAt
            FROM ApiKeys
            WHERE Project = @ProjectName COLLATE NOCASE
            ORDER BY Name
            """,
            new { ProjectName = projectName },
            ct);

        return result.Rows.Select(MapApiKey).ToList();
    }

    public async Task AddAsync(ApiKey apiKey, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            INSERT INTO ApiKeys (Name, Key, Project, Environment, Scope, CreatedAt, UpdatedAt)
            VALUES (@Name, @Key, @Project, @Environment, @Scope, @CreatedAt, @UpdatedAt)
            RETURNING Id
            """,
            ToParameters(apiKey),
            ct);

        apiKey.Id = result.Rows.Count > 0 ? result.Rows[0].GetInt64("Id") : result.LastInsertRowId ?? 0;
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        await _client.ExecuteAsync(
            """
            DELETE FROM ApiKeys
            WHERE Id = @Id
            """,
            new { Id = id },
            ct);
    }

    private static ApiKey MapApiKey(LibsqlRow row)
    {
        return new ApiKey
        {
            Id = row.GetInt64("Id"),
            Name = row.GetString("Name"),
            Key = row.GetString("Key"),
            Project = row.GetString("Project"),
            Environment = row.GetNullableString("Environment"),
            Scope = (KeyScope)row.GetInt32("Scope"),
            CreatedAt = DateTime.Parse(row.GetString("CreatedAt")),
            UpdatedAt = DateTime.Parse(row.GetString("UpdatedAt"))
        };
    }

    private static object ToParameters(ApiKey apiKey)
    {
        return new
        {
            apiKey.Name,
            apiKey.Key,
            apiKey.Project,
            apiKey.Environment,
            Scope = (int)apiKey.Scope,
            CreatedAt = apiKey.CreatedAt.ToString("O"),
            UpdatedAt = apiKey.UpdatedAt.ToString("O")
        };
    }
}
