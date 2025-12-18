namespace Nona.Infrastructure.Services;

public class DateTimeService : IDateTime
{
    public DateTime NowUtc => DateTime.UtcNow;
}
