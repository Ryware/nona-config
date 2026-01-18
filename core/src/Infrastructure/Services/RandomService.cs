namespace Nona.Infrastructure.Services;

public class RandomService : IRandom
{
    private readonly Random _random = Random.Shared;

    public int Next(int min, int max) => _random.Next(min, max);
    public int Next(int max) => _random.Next(int.MinValue, max);
    public int Next() => _random.Next(int.MinValue, int.MaxValue);
}
