using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using Nona.Libsql;

namespace Nona.Infrastructure.Repositories.Libsql;

public sealed class LibsqlAuditLogRepository : IAuditLogRepository
{
    private readonly ILibsqlDatabaseClient _client;

    public LibsqlAuditLogRepository(ILibsqlDatabaseClient client)
    {
        _client = client;
    }

    public async Task AddAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            INSERT INTO AuditLogs (Actor, ActorIsSystem, Action, Target, Project, Environment, CreatedAt)
            VALUES (@Actor, @ActorIsSystem, @Action, @Target, @Project, @Environment, @CreatedAt)
            """,
            ToParameters(entry),
            ct);

        entry.Id = result.LastInsertRowId ?? 0;
    }

    public async Task<IReadOnlyList<AuditLogEntry>> ListAsync(CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT rowid AS Id, Actor, ActorIsSystem, Action, Target, Project, Environment, CreatedAt
            FROM AuditLogs
            ORDER BY CreatedAt DESC, rowid DESC
            """,
            ct: ct);

        return result.Rows.Select(Map).ToList();
    }

    private static AuditLogEntry Map(LibsqlRow row)
    {
        return new AuditLogEntry
        {
            Id = row.GetInt64("Id"),
            Actor = row.GetString("Actor"),
            ActorIsSystem = row.GetBoolean("ActorIsSystem"),
            Action = row.GetString("Action"),
            Target = row.GetString("Target"),
            Project = row.GetNullableString("Project"),
            Environment = row.GetNullableString("Environment"),
            CreatedAt = DateTime.Parse(row.GetString("CreatedAt"))
        };
    }

    private static IReadOnlyDictionary<string, object?> ToParameters(AuditLogEntry entry)
    {
        return LibsqlParameters.Create(
            ("Actor", entry.Actor),
            ("ActorIsSystem", entry.ActorIsSystem),
            ("Action", entry.Action),
            ("Target", entry.Target),
            ("Project", entry.Project),
            ("Environment", entry.Environment),
            ("CreatedAt", entry.CreatedAt.ToString("O")));
    }
}
