using Dapper;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Infrastructure.Repositories.Sqlite;

public class SqliteUserRepository : IUserRepository
{
    private readonly SqliteDbContext _dbContext;

    public SqliteUserRepository(SqliteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetAsync(string email, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT rowid AS Id, Email, Name, PasswordHash, PasswordSalt, Role, Scope, IsAdmin, CreatedAt, UpdatedAt, PasswordResetToken, InviteTokenHash
            FROM Users
            WHERE Email = @Email COLLATE NOCASE
            LIMIT 1";

        var result = await connection.QueryFirstOrDefaultAsync<UserDto>(sql, new { Email = email });

        return result?.ToEntity();
    }

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT rowid AS Id, Email, Name, PasswordHash, PasswordSalt, Role, Scope, IsAdmin, CreatedAt, UpdatedAt, PasswordResetToken, InviteTokenHash
            FROM Users
            ORDER BY Email";

        var results = await connection.QueryAsync<UserDto>(sql);

        return results.Select(dto => dto.ToEntity()).ToList();
    }

    public async Task<User?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT rowid AS Id, Email, Name, PasswordHash, PasswordSalt, Role, Scope, IsAdmin, CreatedAt, UpdatedAt, PasswordResetToken, InviteTokenHash
            FROM Users
            WHERE rowid = @Id
            LIMIT 1";

        var result = await connection.QueryFirstOrDefaultAsync<UserDto>(sql, new { Id = id });

        return result?.ToEntity();
    }

    public async Task<User?> GetByInviteTokenHashAsync(string inviteTokenHash, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT rowid AS Id, Email, Name, PasswordHash, PasswordSalt, Role, Scope, IsAdmin, CreatedAt, UpdatedAt, PasswordResetToken, InviteTokenHash
            FROM Users
            WHERE InviteTokenHash = @InviteTokenHash
            LIMIT 1";

        var result = await connection.QueryFirstOrDefaultAsync<UserDto>(sql, new { InviteTokenHash = inviteTokenHash });

        return result?.ToEntity();
    }

    public async Task<bool> ExistsAsync(string email, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            SELECT COUNT(1)
            FROM Users
            WHERE Email = @Email COLLATE NOCASE";

        var count = await connection.ExecuteScalarAsync<int>(sql, new { Email = email });

        return count > 0;
    }

    public async Task<bool> ExistsAnyAsync(CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"SELECT COUNT(1) FROM Users";

        var count = await connection.ExecuteScalarAsync<int>(sql);

        return count > 0;
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            INSERT INTO Users (Email, Name, PasswordHash, PasswordSalt, Role, Scope, IsAdmin, CreatedAt, UpdatedAt, PasswordResetToken, InviteTokenHash)
            VALUES (@Email, @Name, @PasswordHash, @PasswordSalt, @Role, @Scope, @IsAdmin, @CreatedAt, @UpdatedAt, @PasswordResetToken, @InviteTokenHash)";

        await connection.ExecuteAsync(sql, UserDto.FromEntity(user));

        var id = await connection.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        user.Id = id;
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            UPDATE Users
            SET PasswordHash = @PasswordHash,
                PasswordSalt = @PasswordSalt,
                Name = @Name,
                Role = @Role,
                Scope = @Scope,
                IsAdmin = @IsAdmin,
                UpdatedAt = @UpdatedAt,
                PasswordResetToken = @PasswordResetToken,
                InviteTokenHash = @InviteTokenHash
            WHERE Email = @Email COLLATE NOCASE";

        await connection.ExecuteAsync(sql, UserDto.FromEntity(user));
    }

    public async Task<bool> DeleteAsync(string email, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            DELETE FROM Users
            WHERE Email = @Email COLLATE NOCASE";

        var rowsAffected = await connection.ExecuteAsync(sql, new { Email = email });

        return rowsAffected > 0;
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"SELECT COUNT(*) FROM Users";

        return await connection.QueryFirstAsync<int>(sql);
    }

    private class UserDto
    {
        public long Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
        public string? PasswordSalt { get; set; }
        public int Role { get; set; }
        public int Scope { get; set; }
        public bool IsAdmin { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
        public string? PasswordResetToken { get; set; }
        public string? InviteTokenHash { get; set; }

        public User ToEntity()
        {
            return new User
            {
                Id = Id,
                Email = Email,
                Name = Name,
                PasswordHash = PasswordHash,
                PasswordSalt = PasswordSalt,
                Role = (UserRole)Role,
                Scope = (KeyScope)Scope,
                IsAdmin = IsAdmin,
                CreatedAt = DateTime.Parse(CreatedAt),
                UpdatedAt = DateTime.Parse(UpdatedAt),
                PasswordResetToken = PasswordResetToken,
                InviteTokenHash = InviteTokenHash
            };
        }

        public static UserDto FromEntity(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                PasswordHash = user.PasswordHash,
                PasswordSalt = user.PasswordSalt,
                Role = (int)user.Role,
                Scope = (int)user.Scope,
                IsAdmin = user.IsAdmin,
                CreatedAt = user.CreatedAt.ToString("O"),
                UpdatedAt = user.UpdatedAt.ToString("O"),
                PasswordResetToken = user.PasswordResetToken,
                InviteTokenHash = user.InviteTokenHash
            };
        }
    }
}
