using System.CommandLine;

namespace Nona.Cli;

internal interface ICliCommandGroup
{
    Command Build();
}
