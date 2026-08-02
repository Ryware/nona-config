using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.AuditLogs.Queries;

internal static class AuditLogQueryFilter
{
    public static AuditLogFilter Create(
        string? search,
        string? action,
        string? environment,
        DateOnly? dateFrom,
        DateOnly? dateTo)
    {
        return new AuditLogFilter(
            Normalize(search),
            Normalize(action),
            Normalize(environment),
            dateFrom?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            ToExclusiveDate(dateTo));
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DateTime? ToExclusiveDate(DateOnly? date)
    {
        if (date is null || date.Value == DateOnly.MaxValue)
        {
            return null;
        }

        return date.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }
}
