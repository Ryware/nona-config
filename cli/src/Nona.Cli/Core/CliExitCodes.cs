namespace Nona.Cli;

internal static class CliExitCodes
{
    internal const int Success = 0;
    internal const int UnexpectedError = 1;
    internal const int ValidationError = 2;
    internal const int AuthenticationError = 3;
    internal const int NotFound = 4;
    internal const int Conflict = 5;
    internal const int ServerError = 6;
    internal const int Cancelled = 130;
}
