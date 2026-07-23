using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

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

        var verboseOption = new Option<bool>(
            "--verbose",
            "Show full exception details when a command fails.");
        var root = CreateRootCommand(ctx, verboseOption);

        return await CreateParser(root, verboseOption).InvokeAsync(args);
    }

    internal static RootCommand CreateRootCommand(CliContext ctx, Option<bool> verboseOption)
    {
        var root = new RootCommand("Administer Nona configuration through a command-line interface.");
        root.AddGlobalOption(verboseOption);

        foreach (var type in System.Reflection.Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ICliCommandGroup).IsAssignableFrom(t)))
        {
            var group = (ICliCommandGroup)Activator.CreateInstance(type, ctx)!;
            root.AddCommand(group.Build());
        }

        return root;
    }

    internal static Parser CreateParser(RootCommand root, Option<bool> verboseOption)
        => new CommandLineBuilder(root)
            .UseDefaults()
            .UseExceptionHandler((exception, context) =>
                CliExceptionHandler.Handle(exception, context, verboseOption))
            .Build();
}
