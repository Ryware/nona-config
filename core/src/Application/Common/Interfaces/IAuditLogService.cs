namespace Nona.Application.Common.Interfaces;

public interface IAuditLogService
{
    Task WriteAsync(
        string action,
        string target,
        string? project = null,
        string? environment = null,
        CancellationToken cancellationToken = default);

    Task WriteAsAsync(
        string actor,
        bool actorIsSystem,
        string action,
        string target,
        string? project = null,
        string? environment = null,
        CancellationToken cancellationToken = default);
}
