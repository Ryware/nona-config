using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using Nona.Libsql;
using System.Globalization;

namespace Nona.Infrastructure.Repositories.Libsql;

public sealed class LibsqlParameterShareLinkRepository : IParameterShareLinkRepository
{
    private readonly ILibsqlDatabaseClient _client;

    public LibsqlParameterShareLinkRepository(ILibsqlDatabaseClient client)
    {
        _client = client;
    }

    public async Task<ParameterShareLink?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Id, TokenHash, Token, Project, Environment, Key, CanEdit, CreatedBy, CreatedAt, ExpiresAt, RevokedAt
            FROM ParameterShareLinks
            WHERE Id = @Id
            LIMIT 1
            """,
            LibsqlParameters.Create(("Id", id)),
            ct);

        return result.Rows.Count == 0 ? null : Map(result.Rows[0]);
    }

    public async Task<ParameterShareLink?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Id, TokenHash, Token, Project, Environment, Key, CanEdit, CreatedBy, CreatedAt, ExpiresAt, RevokedAt
            FROM ParameterShareLinks
            WHERE TokenHash = @TokenHash
            LIMIT 1
            """,
            LibsqlParameters.Create(("TokenHash", tokenHash)),
            ct);

        return result.Rows.Count == 0 ? null : Map(result.Rows[0]);
    }

    public async Task<IReadOnlyList<ParameterShareLink>> ListByConfigEntryAsync(
        string projectName,
        string environmentName,
        string key,
        CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Id, TokenHash, Token, Project, Environment, Key, CanEdit, CreatedBy, CreatedAt, ExpiresAt, RevokedAt
            FROM ParameterShareLinks
            WHERE Project = @Project COLLATE NOCASE
              AND Environment = @Environment COLLATE NOCASE
              AND Key = @Key COLLATE NOCASE
            ORDER BY CreatedAt DESC, Id DESC
            """,
            CreateScopeParameters(projectName, environmentName, key),
            ct);

        return result.Rows.Select(Map).ToList();
    }

    public async Task AddAsync(ParameterShareLink shareLink, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            INSERT INTO ParameterShareLinks (
                TokenHash,
                Token,
                Project,
                Environment,
                Key,
                CanEdit,
                CreatedBy,
                CreatedAt,
                ExpiresAt,
                RevokedAt
            )
            VALUES (
                @TokenHash,
                @Token,
                @Project,
                @Environment,
                @Key,
                @CanEdit,
                @CreatedBy,
                @CreatedAt,
                @ExpiresAt,
                @RevokedAt
            )
            RETURNING Id
            """,
            ToParameters(shareLink),
            ct);

        shareLink.Id = result.Rows.Count > 0
            ? result.Rows[0].GetInt64("Id")
            : result.LastInsertRowId ?? 0;
    }

    public async Task RevokeAsync(long id, DateTime revokedAt, CancellationToken ct = default)
    {
        await _client.ExecuteAsync(
            """
            UPDATE ParameterShareLinks
            SET RevokedAt = @RevokedAt
            WHERE Id = @Id
            """,
            LibsqlParameters.Create(
                ("Id", id),
                ("RevokedAt", revokedAt.ToString("O"))),
            ct);
    }

    private static IReadOnlyDictionary<string, object?> CreateScopeParameters(
        string projectName,
        string environmentName,
        string key)
    {
        return LibsqlParameters.Create(
            ("Project", projectName),
            ("Environment", environmentName),
            ("Key", key));
    }

    private static ParameterShareLink Map(LibsqlRow row)
    {
        return new ParameterShareLink
        {
            Id = row.GetInt64("Id"),
            TokenHash = row.GetString("TokenHash"),
            Token = row.GetNullableString("Token") ?? string.Empty,
            Project = row.GetString("Project"),
            Environment = row.GetString("Environment"),
            Key = row.GetString("Key"),
            CanEdit = row.GetBoolean("CanEdit"),
            CreatedBy = row.GetString("CreatedBy"),
            CreatedAt = ParseTimestamp(row.GetString("CreatedAt")),
            ExpiresAt = ParseTimestamp(row.GetString("ExpiresAt")),
            RevokedAt = ParseNullableTimestamp(row.GetNullableString("RevokedAt"))
        };
    }

    private static IReadOnlyDictionary<string, object?> ToParameters(ParameterShareLink shareLink)
    {
        return LibsqlParameters.Create(
            ("TokenHash", shareLink.TokenHash),
            ("Token", shareLink.Token),
            ("Project", shareLink.Project),
            ("Environment", shareLink.Environment),
            ("Key", shareLink.Key),
            ("CanEdit", shareLink.CanEdit),
            ("CreatedBy", shareLink.CreatedBy),
            ("CreatedAt", shareLink.CreatedAt.ToString("O")),
            ("ExpiresAt", shareLink.ExpiresAt.ToString("O")),
            ("RevokedAt", shareLink.RevokedAt?.ToString("O")));
    }

    private static DateTime ParseTimestamp(string value)
        => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static DateTime? ParseNullableTimestamp(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : ParseTimestamp(value);
}
