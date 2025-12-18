namespace Nona.Infrastructure.Services;

public class GuidGeneratorService : IGuidGenerator
{
    public Guid NewGuid()
    {
        return Guid.NewGuid();
    }
}
