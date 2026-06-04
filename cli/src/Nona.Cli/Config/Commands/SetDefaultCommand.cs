namespace Nona.Cli.Config.Commands;

internal sealed record SetDefaultCommand(string Name, string Value);

internal sealed class SetDefaultCommandHandler(CliDefaultsStore defaultsStore)
{
    public Task<int> HandleAsync(SetDefaultCommand command, CancellationToken ct)
    {
        var current = defaultsStore.Load();
        var updated = command.Name switch
        {
            "base-url" => current with { BaseUrl = command.Value },
            "project"  => current with { Project = command.Value },
            _          => throw new InvalidOperationException($"Unsupported setting '{command.Name}'.")
        };

        defaultsStore.Save(updated);
        Console.WriteLine($"Saved {command.Name}: {command.Value}");
        return Task.FromResult(0);
    }
}
