using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using Nona.Libsql;

namespace Nona.Infrastructure.Repositories.Libsql;

public sealed class LibsqlExternalIdentityRepository(ILibsqlDatabaseClient client) : IExternalIdentityRepository
{
    public async Task<ExternalIdentity?> GetAsync(string provider, string issuer, string subject, CancellationToken ct = default)
    {
        var result = await client.ExecuteAsync(
            """
            SELECT rowid AS Id, Provider, Issuer, Subject, UserEmail, CreatedAt, UpdatedAt, LastLoginAt
            FROM ExternalIdentities
            WHERE Provider = @Provider COLLATE NOCASE
              AND Issuer = @Issuer COLLATE NOCASE
              AND Subject = @Subject COLLATE NOCASE
            LIMIT 1
            """,
            new
            {
                Provider = provider,
                Issuer = issuer,
                Subject = subject
            },
            ct);

        return result.Rows.Count == 0 ? null : Map(result.Rows[0]);
    }

    public async Task<IReadOnlyList<ExternalIdentity>> ListAsync(CancellationToken ct = default)
    {
        var result = await client.ExecuteAsync(
            """
            SELECT rowid AS Id, Provider, Issuer, Subject, UserEmail, CreatedAt, UpdatedAt, LastLoginAt
            FROM ExternalIdentities
            ORDER BY Provider, Issuer, Subject
            """,
            ct: ct);

        return result.Rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ExternalIdentity>> ListByUserEmailAsync(string userEmail, CancellationToken ct = default)
    {
        var result = await client.ExecuteAsync(
            """
            SELECT rowid AS Id, Provider, Issuer, Subject, UserEmail, CreatedAt, UpdatedAt, LastLoginAt
            FROM ExternalIdentities
            WHERE UserEmail = @UserEmail COLLATE NOCASE
            ORDER BY Provider, Issuer, Subject
            """,
            new { UserEmail = userEmail },
            ct);

        return result.Rows.Select(Map).ToList();
    }

    public async Task AddAsync(ExternalIdentity identity, CancellationToken ct = default)
    {
        var result = await client.ExecuteAsync(
            """
            INSERT INTO ExternalIdentities (Provider, Issuer, Subject, UserEmail, CreatedAt, UpdatedAt, LastLoginAt)
            VALUES (@Provider, @Issuer, @Subject, @UserEmail, @CreatedAt, @UpdatedAt, @LastLoginAt)
            """,
            ToParameters(identity),
            ct);

        identity.Id = result.LastInsertRowId ?? 0;
    }

    public async Task UpdateAsync(ExternalIdentity identity, CancellationToken ct = default)
    {
        await client.ExecuteAsync(
            """
            UPDATE ExternalIdentities
            SET UserEmail = @UserEmail,
                UpdatedAt = @UpdatedAt,
                LastLoginAt = @LastLoginAt
            WHERE Provider = @Provider COLLATE NOCASE
              AND Issuer = @Issuer COLLATE NOCASE
              AND Subject = @Subject COLLATE NOCASE
            """,
            ToParameters(identity),
            ct);
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        var result = await client.ExecuteAsync("SELECT COUNT(*) FROM ExternalIdentities", ct: ct);
        return result.Rows[0].GetInt32(0);
    }

    private static ExternalIdentity Map(LibsqlRow row)
    {
        return new ExternalIdentity
        {
            Id = row.GetInt64("Id"),
            Provider = row.GetString("Provider"),
            Issuer = row.GetString("Issuer"),
            Subject = row.GetString("Subject"),
            UserEmail = row.GetString("UserEmail"),
            CreatedAt = DateTime.Parse(row.GetString("CreatedAt")),
            UpdatedAt = DateTime.Parse(row.GetString("UpdatedAt")),
            LastLoginAt = string.IsNullOrWhiteSpace(row.GetNullableString("LastLoginAt"))
                ? null
                : DateTime.Parse(row.GetString("LastLoginAt"))
        };
    }

    private static object ToParameters(ExternalIdentity identity)
    {
        return new
        {
            identity.Provider,
            identity.Issuer,
            identity.Subject,
            identity.UserEmail,
            CreatedAt = identity.CreatedAt.ToString("O"),
            UpdatedAt = identity.UpdatedAt.ToString("O"),
            LastLoginAt = identity.LastLoginAt?.ToString("O")
        };
    }
}
