namespace Nona.Domain.Enums;

[Flags]
public enum KeyScope
{
    Backend = 1,
    Frontend = 2,
    All = Backend | Frontend
}
