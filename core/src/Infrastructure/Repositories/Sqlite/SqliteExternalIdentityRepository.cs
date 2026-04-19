using Dapper;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Infrastructure.Repositories.Sqlite;

public sealed class SqliteExternalIdentityRepository(SqliteDbContext dbContext) : IExternalIdentityRepository
{
    public async Task<ExternalIdentity?> GetAsync(string provider, string issuer, string subject, CancellationToken ct = default)
    {
        var connection = await dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT rowid AS Id, Provider, Issuer, Subject, UserEmail, CreatedAt, UpdatedAt, LastLoginAt
            FROM ExternalIdentities
            WHERE Provider = @Provider COLLATE NOCASE
              AND Issuer = @Issuer COLLATE NOCASE
              AND Subject = @Subject COLLATE NOCASE
            LIMIT 1";

        var result = await connection.QueryFirstOrDefaultAsync<ExternalIdentityDto>(sql, new
        {
            Provider = provider,
            Issuer = issuer,
            Subject = subject
        });

        return result?.ToEntity();
    }

    public async Task<IReadOnlyList<ExternalIdentity>> ListAsync(CancellationToken ct = default)
    {
        var connection = await dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT rowid AS Id, Provider, Issuer, Subject, UserEmail, CreatedAt, UpdatedAt, LastLoginAt
            FROM ExternalIdentities
            ORDER BY Provider, Issuer, Subject";

        var results = await connection.QueryAsync<ExternalIdentityDto>(sql);
        return results.Select(result => result.ToEntity()).ToList();
    }

    public async Task<IReadOnlyList<ExternalIdentity>> ListByUserEmailAsync(string userEmail, CancellationToken ct = default)
    {
        var connection = await dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT rowid AS Id, Provider, Issuer, Subject, UserEmail, CreatedAt, UpdatedAt, LastLoginAt
            FROM ExternalIdentities
            WHERE UserEmail = @UserEmail COLLATE NOCASE
            ORDER BY Provider, Issuer, Subject";

        var results = await connection.QueryAsync<ExternalIdentityDto>(sql, new { UserEmail = userEmail });
        return results.Select(result => result.ToEntity()).ToList();
    }

    public async Task AddAsync(ExternalIdentity identity, CancellationToken ct = default)
    {
        var connection = await dbContext.GetConnectionAsync(ct);

        var sql = @"
            INSERT INTO ExternalIdentities (Provider, Issuer, Subject, UserEmail, CreatedAt, UpdatedAt, LastLoginAt)
            VALUES (@Provider, @Issuer, @Subject, @UserEmail, @CreatedAt, @UpdatedAt, @LastLoginAt)";

        await connection.ExecuteAsync(sql, ExternalIdentityDto.FromEntity(identity));
        identity.Id = await connection.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
    }

    public async Task UpdateAsync(ExternalIdentity identity, CancellationToken ct = default)
    {
        var connection = await dbContext.GetConnectionAsync(ct);

        var sql = @"
            UPDATE ExternalIdentities
            SET UserEmail = @UserEmail,
                UpdatedAt = @UpdatedAt,
                LastLoginAt = @LastLoginAt
            WHERE Provider = @Provider COLLATE NOCASE
              AND Issuer = @Issuer COLLATE NOCASE
              AND Subject = @Subject COLLATE NOCASE";

        await connection.ExecuteAsync(sql, ExternalIdentityDto.FromEntity(identity));
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        var connection = await dbContext.GetConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ExternalIdentities");
    }

    private sealed class ExternalIdentityDto
    {
        public long Id { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
        public string? LastLoginAt { get; set; }

        public ExternalIdentity ToEntity()
        {
            return new ExternalIdentity
            {
                Id = Id,
                Provider = Provider,
                Issuer = Issuer,
                Subject = Subject,
                UserEmail = UserEmail,
                CreatedAt = DateTime.Parse(CreatedAt),
                UpdatedAt = DateTime.Parse(UpdatedAt),
                LastLoginAt = string.IsNullOrWhiteSpace(LastLoginAt) ? null : DateTime.Parse(LastLoginAt)
            };
        }

        public static ExternalIdentityDto FromEntity(ExternalIdentity identity)
        {
            return new ExternalIdentityDto
            {
                Id = identity.Id,
                Provider = identity.Provider,
                Issuer = identity.Issuer,
                Subject = identity.Subject,
                UserEmail = identity.UserEmail,
                CreatedAt = identity.CreatedAt.ToString("O"),
                UpdatedAt = identity.UpdatedAt.ToString("O"),
                LastLoginAt = identity.LastLoginAt?.ToString("O")
            };
        }
    }
}
