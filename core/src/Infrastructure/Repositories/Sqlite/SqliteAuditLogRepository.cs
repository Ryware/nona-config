using Dapper;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Infrastructure.Repositories.Sqlite;

public sealed class SqliteAuditLogRepository : IAuditLogRepository
{
    private readonly SqliteDbContext _dbContext;

    public SqliteAuditLogRepository(SqliteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = """
            INSERT INTO AuditLogs (Actor, ActorIsSystem, Action, Target, Project, Environment, CreatedAt)
            VALUES (@Actor, @ActorIsSystem, @Action, @Target, @Project, @Environment, @CreatedAt)
            """;

        await connection.ExecuteAsync(sql, AuditLogDto.FromEntity(entry));

        var id = await connection.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        entry.Id = id;
    }

    public async Task<IReadOnlyList<AuditLogEntry>> ListAsync(CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        var sql = """
            SELECT rowid AS Id, Actor, ActorIsSystem, Action, Target, Project, Environment, CreatedAt
            FROM AuditLogs
            ORDER BY CreatedAt DESC, rowid DESC
            """;

        var results = await connection.QueryAsync<AuditLogDto>(sql);
        return results.Select(dto => dto.ToEntity()).ToList();
    }

    private sealed class AuditLogDto
    {
        public long Id { get; set; }

        public string Actor { get; set; } = string.Empty;

        public bool ActorIsSystem { get; set; }

        public string Action { get; set; } = string.Empty;

        public string Target { get; set; } = string.Empty;

        public string? Project { get; set; }

        public string? Environment { get; set; }

        public string CreatedAt { get; set; } = string.Empty;

        public AuditLogEntry ToEntity()
        {
            return new AuditLogEntry
            {
                Id = Id,
                Actor = Actor,
                ActorIsSystem = ActorIsSystem,
                Action = Action,
                Target = Target,
                Project = Project,
                Environment = Environment,
                CreatedAt = DateTime.Parse(CreatedAt)
            };
        }

        public static AuditLogDto FromEntity(AuditLogEntry entry)
        {
            return new AuditLogDto
            {
                Id = entry.Id,
                Actor = entry.Actor,
                ActorIsSystem = entry.ActorIsSystem,
                Action = entry.Action,
                Target = entry.Target,
                Project = entry.Project,
                Environment = entry.Environment,
                CreatedAt = entry.CreatedAt.ToString("O")
            };
        }
    }
}
