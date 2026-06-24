using System.CommandLine;

namespace Nona.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (CliVersion.IsVersionRequest(args))
        {
            Console.Out.WriteLine(CliVersion.GetDisplayVersion());
            return 0;
        }

        var defaultsStore = new CliDefaultsStore();
        var sessionStore = new CliSessionStore();

        var ctx = new CliContext(
            defaults: defaultsStore.Load(),
            session: sessionStore.Load(),
            defaultsStore: defaultsStore,
            sessionStore: sessionStore);

        var root = new RootCommand("Nona CLI for key management and Firebase Remote Config migrations.");

        foreach (var type in Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ICliCommandGroup).IsAssignableFrom(t)))
        {
            var group = (ICliCommandGroup)Activator.CreateInstance(type, ctx)!;
            root.AddCommand(group.Build());
        }

        try
        {
            return await root.InvokeAsync(args);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
