namespace Nona.Cli;

internal sealed class CliContext
{
    public CliContext(
        CliDefaults defaults,
        CliAuthSession? session,
        CliDefaultsStore defaultsStore,
        CliSessionStore sessionStore)
    {
        Defaults = defaults;
        Session = session;
        DefaultsStore = defaultsStore;
        SessionStore = sessionStore;
        Resolver = new CliValueResolver(defaults, session);
    }

    public CliDefaults Defaults { get; }
    public CliAuthSession? Session { get; }
    public CliDefaultsStore DefaultsStore { get; }
    public CliSessionStore SessionStore { get; }
    public CliValueResolver Resolver { get; }
}
