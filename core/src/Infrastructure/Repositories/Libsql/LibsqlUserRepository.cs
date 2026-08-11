using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using Nona.Libsql;
using System.Globalization;

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
            SELECT rowid AS Id, Email, Name, PasswordHash, PasswordSalt, Role, Scope, CreatedAt, UpdatedAt, InviteTokenHash, PasswordResetTokenHash, PasswordResetTokenExpiresAt
            FROM Users
            WHERE Email = @Email COLLATE NOCASE
            LIMIT 1
            """,
            LibsqlParameters.Create(("Email", email)),
            ct);

        return result.Rows.Count == 0 ? null : Map(result.Rows[0]);
    }

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT rowid AS Id, Email, Name, PasswordHash, PasswordSalt, Role, Scope, CreatedAt, UpdatedAt, InviteTokenHash, PasswordResetTokenHash, PasswordResetTokenExpiresAt
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
            SELECT rowid AS Id, Email, Name, PasswordHash, PasswordSalt, Role, Scope, CreatedAt, UpdatedAt, InviteTokenHash, PasswordResetTokenHash, PasswordResetTokenExpiresAt
            FROM Users
            WHERE rowid = @Id
            LIMIT 1
            """,
            LibsqlParameters.Create(("Id", id)),
            ct);

        return result.Rows.Count == 0 ? null : Map(result.Rows[0]);
    }

    public async Task<User?> GetByInviteTokenHashAsync(string inviteTokenHash, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT rowid AS Id, Email, Name, PasswordHash, PasswordSalt, Role, Scope, CreatedAt, UpdatedAt, InviteTokenHash, PasswordResetTokenHash, PasswordResetTokenExpiresAt
            FROM Users
            WHERE InviteTokenHash = @InviteTokenHash
            LIMIT 1
            """,
            LibsqlParameters.Create(("InviteTokenHash", inviteTokenHash)),
            ct);

        return result.Rows.Count == 0 ? null : Map(result.Rows[0]);
    }

    public async Task<User?> GetByPasswordResetTokenHashAsync(string passwordResetTokenHash, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT rowid AS Id, Email, Name, PasswordHash, PasswordSalt, Role, Scope, CreatedAt, UpdatedAt, InviteTokenHash, PasswordResetTokenHash, PasswordResetTokenExpiresAt
            FROM Users
            WHERE PasswordResetTokenHash = @PasswordResetTokenHash
            LIMIT 1
            """,
            LibsqlParameters.Create(("PasswordResetTokenHash", passwordResetTokenHash)),
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
            LibsqlParameters.Create(("Email", email)),
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
        var columns = new List<string>
        {
            "Email",
            "Name",
            "Role",
            "Scope",
            "CreatedAt",
            "UpdatedAt"
        };
        var parameters = new Dictionary<string, object?>
        {
            ["Email"] = user.Email,
            ["Name"] = user.Name,
            ["Role"] = (int)user.Role,
            ["Scope"] = (int)user.Scope,
            ["CreatedAt"] = user.CreatedAt.ToString("O"),
            ["UpdatedAt"] = user.UpdatedAt.ToString("O")
        };

        AddOptionalColumn(columns, parameters, "PasswordHash", user.PasswordHash);
        AddOptionalColumn(columns, parameters, "PasswordSalt", user.PasswordSalt);
        AddOptionalColumn(columns, parameters, "InviteTokenHash", user.InviteTokenHash);
        AddOptionalColumn(columns, parameters, "PasswordResetTokenHash", user.PasswordResetTokenHash);
        AddOptionalColumn(
            columns,
            parameters,
            "PasswordResetTokenExpiresAt",
            user.PasswordResetTokenExpiresAt?.ToString("O"));

        var values = columns.Select(column => $"@{column}");
        var sql = $"""
            INSERT INTO Users ({string.Join(", ", columns)})
            VALUES ({string.Join(", ", values)})
            """;

        var result = await _client.ExecuteAsync(sql, parameters, ct);

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
                UpdatedAt = @UpdatedAt,
                InviteTokenHash = @InviteTokenHash,
                PasswordResetTokenHash = @PasswordResetTokenHash,
                PasswordResetTokenExpiresAt = @PasswordResetTokenExpiresAt
            WHERE Email = @Email COLLATE NOCASE
            """,
            ToParameters(user),
            ct);
    }

    public async Task<bool> TryResetPasswordAsync(
        string passwordResetTokenHash,
        DateTime nowUtc,
        string passwordHash,
        string passwordSalt,
        DateTime updatedAt,
        CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            UPDATE Users
            SET PasswordHash = @PasswordHash,
                PasswordSalt = @PasswordSalt,
                PasswordResetTokenHash = NULL,
                PasswordResetTokenExpiresAt = NULL,
                UpdatedAt = @UpdatedAt
            WHERE PasswordResetTokenHash = @PasswordResetTokenHash
              AND PasswordResetTokenExpiresAt IS NOT NULL
              AND PasswordResetTokenExpiresAt > @NowUtc
            """,
            LibsqlParameters.Create(
                ("PasswordHash", passwordHash),
                ("PasswordSalt", passwordSalt),
                ("PasswordResetTokenHash", passwordResetTokenHash),
                ("NowUtc", nowUtc.ToString("O")),
                ("UpdatedAt", updatedAt.ToString("O"))),
            ct);

        return result.AffectedRowCount > 0;
    }

    public async Task<bool> DeleteAsync(string email, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            DELETE FROM Users
            WHERE Email = @Email COLLATE NOCASE
            """,
            LibsqlParameters.Create(("Email", email)),
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
            CreatedAt = ParseUtc(row.GetString("CreatedAt")),
            UpdatedAt = ParseUtc(row.GetString("UpdatedAt")),
            InviteTokenHash = row.GetNullableString("InviteTokenHash"),
            PasswordResetTokenHash = row.GetNullableString("PasswordResetTokenHash"),
            PasswordResetTokenExpiresAt = row.GetNullableString("PasswordResetTokenExpiresAt") is { } expiresAt
                ? ParseUtc(expiresAt)
                : null
        };
    }

    private static DateTime ParseUtc(string value)
    {
        return DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal).UtcDateTime;
    }

    private static IReadOnlyDictionary<string, object?> ToParameters(User user)
    {
        return LibsqlParameters.Create(
            ("Email", user.Email),
            ("Name", user.Name),
            ("PasswordHash", user.PasswordHash),
            ("PasswordSalt", user.PasswordSalt),
            ("Role", (int)user.Role),
            ("Scope", (int)user.Scope),
            ("UpdatedAt", user.UpdatedAt.ToString("O")),
            ("InviteTokenHash", user.InviteTokenHash),
            ("PasswordResetTokenHash", user.PasswordResetTokenHash),
            ("PasswordResetTokenExpiresAt", user.PasswordResetTokenExpiresAt?.ToString("O")));
    }

    private static void AddOptionalColumn(
        ICollection<string> columns,
        IDictionary<string, object?> parameters,
        string column,
        object? value)
    {
        if (value is null)
        {
            return;
        }

        columns.Add(column);
        parameters[column] = value;
    }
}
