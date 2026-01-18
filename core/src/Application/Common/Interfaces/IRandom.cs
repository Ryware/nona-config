namespace Nona.Application.Common.Interfaces;

public interface IRandom
{
    int Next(int min, int max);
    int Next(int max);
    int Next();
}
