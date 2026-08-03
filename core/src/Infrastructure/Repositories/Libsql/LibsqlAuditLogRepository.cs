using System.Globalization;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
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
            INSERT INTO AuditLogs (Actor, ActorIsSystem, ActionKind, Action, Target, Project, Environment, CreatedAt)
            VALUES (@Actor, @ActorIsSystem, @ActionKind, @Action, @Target, @Project, @Environment, @CreatedAt)
            """,
            ToParameters(entry),
            ct);

        entry.Id = result.LastInsertRowId ?? 0;
    }

    public async Task<AuditLogPageResult> ListAsync(AuditLogPageRequest request, CancellationToken ct = default)
    {
        var parameters = ToParameters(request);
        var filterParameters = ToParameters(request.Filter);
        var entriesResult = await _client.ExecuteAsync(
            $"""
            SELECT rowid AS Id, Actor, ActorIsSystem, ActionKind, Action, Target, Project, Environment, CreatedAt
            FROM AuditLogs
            {FilterSql}
            ORDER BY CreatedAt DESC, rowid DESC
            LIMIT @Limit OFFSET @Offset
            """,
            parameters,
            ct);
        var countResult = await _client.ExecuteAsync(
            $"SELECT COUNT(*) AS TotalCount FROM AuditLogs {FilterSql}",
            filterParameters,
            ct);
        var actionsResult = await _client.ExecuteAsync(
            """
            SELECT DISTINCT Action
            FROM AuditLogs
            ORDER BY Action COLLATE NOCASE
            """,
            ct: ct);
        var environmentsResult = await _client.ExecuteAsync(
            """
            SELECT DISTINCT CASE
                WHEN Environment IS NULL OR trim(Environment) = '' THEN @GlobalScopeEnvironment
                ELSE Environment
            END AS Environment
            FROM AuditLogs
            ORDER BY Environment COLLATE NOCASE
            """,
            LibsqlParameters.Create(
                ("GlobalScopeEnvironment", AuditLogFilter.GlobalScopeEnvironment)),
            ct: ct);

        return new AuditLogPageResult(
            entriesResult.Rows.Select(Map).ToList(),
            checked((int)countResult.Rows.Single().GetInt64("TotalCount")),
            actionsResult.Rows.Select(row => row.GetString("Action")).ToList(),
            environmentsResult.Rows.Select(row => row.GetString("Environment")).ToList());
    }

    public async Task<IReadOnlyList<AuditLogEntry>> ListBatchAsync(
        AuditLogBatchRequest request,
        CancellationToken ct = default)
    {
        var cursorSql = request.BeforeCreatedAt is not null && request.BeforeId is not null
            ? """
                AND (CreatedAt < @BeforeCreatedAt OR
                     (CreatedAt = @BeforeCreatedAt AND rowid > @BeforeId))
              """
            : string.Empty;
        var result = await _client.ExecuteAsync(
            $"""
            SELECT rowid AS Id, Actor, ActorIsSystem, ActionKind, Action, Target, Project, Environment, CreatedAt
            FROM AuditLogs
            {FilterSql}
            {cursorSql}
            ORDER BY CreatedAt DESC, rowid ASC
            LIMIT @Limit
            """,
            ToParameters(request),
            ct);

        return result.Rows.Select(Map).ToList();
    }

    private const string FilterSql = """
        WHERE (@Search IS NULL OR
               instr(lower(Actor), lower(@Search)) > 0 OR
               instr(lower(Target), lower(@Search)) > 0 OR
               instr(lower(coalesce(Project, '')), lower(@Search)) > 0)
          AND (@Action IS NULL OR Action = @Action COLLATE NOCASE)
          AND (@Environment IS NULL OR
               (@Environment = @GlobalScopeEnvironment AND
                (Environment IS NULL OR trim(Environment) = '')) OR
               (@Environment <> @GlobalScopeEnvironment AND
                Environment = @Environment COLLATE NOCASE))
          AND (@CreatedFrom IS NULL OR CreatedAt >= @CreatedFrom)
          AND (@CreatedToExclusive IS NULL OR CreatedAt < @CreatedToExclusive)
        """;

    private static AuditLogEntry Map(LibsqlRow row)
    {
        return new AuditLogEntry
        {
            Id = row.GetInt64("Id"),
            Actor = row.GetString("Actor"),
            ActorIsSystem = row.GetBoolean("ActorIsSystem"),
            ActionKind = ParseActionKind(row.GetString("ActionKind")),
            Action = row.GetString("Action"),
            Target = row.GetString("Target"),
            Project = row.GetNullableString("Project"),
            Environment = row.GetNullableString("Environment"),
            CreatedAt = DateTime.Parse(
                row.GetString("CreatedAt"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind)
        };
    }

    private static IReadOnlyDictionary<string, object?> ToParameters(AuditLogEntry entry)
    {
        return LibsqlParameters.Create(
            ("Actor", entry.Actor),
            ("ActorIsSystem", entry.ActorIsSystem),
            ("ActionKind", entry.ActionKind.ToString().ToLowerInvariant()),
            ("Action", entry.Action),
            ("Target", entry.Target),
            ("Project", entry.Project),
            ("Environment", entry.Environment),
            ("CreatedAt", entry.CreatedAt.ToString("O")));
    }

    private static IReadOnlyDictionary<string, object?> ToParameters(AuditLogPageRequest request)
    {
        return LibsqlParameters.Create(
            ("Search", Normalize(request.Filter.Search)),
            ("Action", Normalize(request.Filter.Action)),
            ("Environment", Normalize(request.Filter.Environment)),
            ("GlobalScopeEnvironment", AuditLogFilter.GlobalScopeEnvironment),
            ("CreatedFrom", request.Filter.CreatedFrom?.ToString("O")),
            ("CreatedToExclusive", request.Filter.CreatedToExclusive?.ToString("O")),
            ("Limit", request.Limit),
            ("Offset", request.Offset));
    }

    private static IReadOnlyDictionary<string, object?> ToParameters(AuditLogFilter filter)
    {
        return LibsqlParameters.Create(
            ("Search", Normalize(filter.Search)),
            ("Action", Normalize(filter.Action)),
            ("Environment", Normalize(filter.Environment)),
            ("GlobalScopeEnvironment", AuditLogFilter.GlobalScopeEnvironment),
            ("CreatedFrom", filter.CreatedFrom?.ToString("O")),
            ("CreatedToExclusive", filter.CreatedToExclusive?.ToString("O")));
    }

    private static IReadOnlyDictionary<string, object?> ToParameters(AuditLogBatchRequest request)
    {
        return LibsqlParameters.Create(
            ("Search", Normalize(request.Filter.Search)),
            ("Action", Normalize(request.Filter.Action)),
            ("Environment", Normalize(request.Filter.Environment)),
            ("GlobalScopeEnvironment", AuditLogFilter.GlobalScopeEnvironment),
            ("CreatedFrom", request.Filter.CreatedFrom?.ToString("O")),
            ("CreatedToExclusive", request.Filter.CreatedToExclusive?.ToString("O")),
            ("BeforeCreatedAt", request.BeforeCreatedAt?.ToString("O")),
            ("BeforeId", request.BeforeId),
            ("Limit", request.Limit));
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static AuditActionKind ParseActionKind(string value)
    {
        return Enum.TryParse<AuditActionKind>(value, ignoreCase: true, out var actionKind)
            ? actionKind
            : AuditActionKind.Activity;
    }
}
