namespace Nona.Libsql;

public interface ILibsqlDatabaseClient
{
    Task<LibsqlQueryResult> ExecuteAsync(
        string sql,
        object? parameters = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<LibsqlQueryResult>> ExecuteBatchAsync(
        IEnumerable<LibsqlStatement> statements,
        CancellationToken ct = default);
}
