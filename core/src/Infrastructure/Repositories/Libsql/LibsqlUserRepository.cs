using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using Nona.Libsql;

namespace Nona.Infrastructure.Repositories.Libsql;

public sealed class LibsqlUserRepository : IUserRepository
{
    private readonly ILibsqlDatabaseClient _client;

    public LibsqlUserRepository(ILibsqlDatabaseClient client)
    {
        _client = client;
    }

    public async Task<User?> GetAsync(string email, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT rowid AS Id, Email, Name, PasswordHash, PasswordSalt, Role, Scope, IsAdmin, CreatedAt, UpdatedAt, PasswordResetToken
            FROM Users
            WHERE Email = @Email COLLATE NOCASE
            LIMIT 1
            """,
            new { Email = email },
            ct);

        return result.Rows.Count == 0 ? null : Map(result.Rows[0]);
    }

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT rowid AS Id, Email, Name, PasswordHash, PasswordSalt, Role, Scope, IsAdmin, CreatedAt, UpdatedAt, PasswordResetToken
            FROM Users
            ORDER BY Email
            """,
            ct: ct);

        return result.Rows.Select(Map).ToList();
    }

    public async Task<User?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT rowid AS Id, Email, Name, PasswordHash, PasswordSalt, Role, Scope, IsAdmin, CreatedAt, UpdatedAt, PasswordResetToken
            FROM Users
            WHERE rowid = @Id
            LIMIT 1
            """,
            new { Id = id },
            ct);

        return result.Rows.Count == 0 ? null : Map(result.Rows[0]);
    }

    public async Task<bool> ExistsAsync(string email, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT COUNT(1)
            FROM Users
            WHERE Email = @Email COLLATE NOCASE
            """,
            new { Email = email },
            ct);

        return result.Rows[0].GetInt32(0) > 0;
    }

    public async Task<bool> ExistsAnyAsync(CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync("SELECT COUNT(1) FROM Users", ct: ct);
        return result.Rows[0].GetInt32(0) > 0;
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            INSERT INTO Users (Email, Name, PasswordHash, PasswordSalt, Role, Scope, IsAdmin, CreatedAt, UpdatedAt, PasswordResetToken)
            VALUES (@Email, @Name, @PasswordHash, @PasswordSalt, @Role, @Scope, @IsAdmin, @CreatedAt, @UpdatedAt, @PasswordResetToken)
            """,
            ToParameters(user),
            ct);

        user.Id = result.LastInsertRowId ?? 0;
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        await _client.ExecuteAsync(
            """
            UPDATE Users
            SET PasswordHash = @PasswordHash,
                PasswordSalt = @PasswordSalt,
                Name = @Name,
                Role = @Role,
                Scope = @Scope,
                IsAdmin = @IsAdmin,
                UpdatedAt = @UpdatedAt,
                PasswordResetToken = @PasswordResetToken
            WHERE Email = @Email COLLATE NOCASE
            """,
            ToParameters(user),
            ct);
    }

    public async Task<bool> DeleteAsync(string email, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            DELETE FROM Users
            WHERE Email = @Email COLLATE NOCASE
            """,
            new { Email = email },
            ct);

        return result.AffectedRowCount > 0;
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync("SELECT COUNT(*) FROM Users", ct: ct);
        return result.Rows[0].GetInt32(0);
    }

    private static User Map(LibsqlRow row)
    {
        return new User
        {
            Id = row.GetInt64("Id"),
            Email = row.GetString("Email"),
            Name = row.GetString("Name"),
            PasswordHash = row.GetNullableString("PasswordHash"),
            PasswordSalt = row.GetNullableString("PasswordSalt"),
            Role = (UserRole)row.GetInt32("Role"),
            Scope = (KeyScope)row.GetInt32("Scope"),
            IsAdmin = row.GetBoolean("IsAdmin"),
            CreatedAt = DateTime.Parse(row.GetString("CreatedAt")),
            UpdatedAt = DateTime.Parse(row.GetString("UpdatedAt")),
            PasswordResetToken = row.GetNullableString("PasswordResetToken")
        };
    }

    private static object ToParameters(User user)
    {
        return new
        {
            user.Email,
            user.Name,
            user.PasswordHash,
            user.PasswordSalt,
            Role = (int)user.Role,
            Scope = (int)user.Scope,
            user.IsAdmin,
            CreatedAt = user.CreatedAt.ToString("O"),
            UpdatedAt = user.UpdatedAt.ToString("O"),
            user.PasswordResetToken
        };
    }
}
