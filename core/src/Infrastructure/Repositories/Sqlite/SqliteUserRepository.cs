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
            SELECT Email, PasswordHash, PasswordSalt, Role, Scope, CreatedAt, UpdatedAt, PasswordResetToken
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
            SELECT Email, PasswordHash, PasswordSalt, Role, Scope, CreatedAt, UpdatedAt, PasswordResetToken
            FROM Users
            ORDER BY Email";

        var results = await connection.QueryAsync<UserDto>(sql);

        return results.Select(dto => dto.ToEntity()).ToList();
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
            INSERT INTO Users (Email, PasswordHash, PasswordSalt, Role, Scope, CreatedAt, UpdatedAt, PasswordResetToken)
            VALUES (@Email, @PasswordHash, @PasswordSalt, @Role, @Scope, @CreatedAt, @UpdatedAt, @PasswordResetToken)";

        await connection.ExecuteAsync(sql, UserDto.FromEntity(user));
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = @"
            UPDATE Users
            SET PasswordHash = @PasswordHash,
                PasswordSalt = @PasswordSalt,
                Role = @Role,
                Scope = @Scope,
                UpdatedAt = @UpdatedAt,
                PasswordResetToken = @PasswordResetToken
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

    private class UserDto
    {
        public string Email { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
        public string? PasswordSalt { get; set; }
        public int Role { get; set; }
        public int Scope { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
        public string? PasswordResetToken { get; set; }

        public User ToEntity()
        {
            return new User
            {
                Email = Email,
                PasswordHash = PasswordHash,
                PasswordSalt = PasswordSalt,
                Role = (UserRole)Role,
                Scope = (KeyScope)Scope,
                CreatedAt = DateTime.Parse(CreatedAt),
                UpdatedAt = DateTime.Parse(UpdatedAt),
                PasswordResetToken = PasswordResetToken
            };
        }

        public static UserDto FromEntity(User user)
        {
            return new UserDto
            {
                Email = user.Email,
                PasswordHash = user.PasswordHash,
                PasswordSalt = user.PasswordSalt,
                Role = (int)user.Role,
                Scope = (int)user.Scope,
                CreatedAt = user.CreatedAt.ToString("O"),
                UpdatedAt = user.UpdatedAt.ToString("O"),
                PasswordResetToken = user.PasswordResetToken
            };
        }
    }
}
