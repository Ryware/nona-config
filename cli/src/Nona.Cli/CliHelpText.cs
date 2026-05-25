namespace Nona.Cli;

internal static class CliHelpText
{
    public static string Value =>
        """
        Nona CLI

        Usage:
          nona auth login [--base-url <url>] [--email <email>] [--password <password>]
          nona auth logout
          nona auth whoami
          nona config show
          nona config set <base-url|project> <value>
          nona keys show --project <name> --base-url <url> (--token <token> | --email <email> --password <password>)
          nona keys reroll --project <name> --type <server|client|both> --base-url <url> (--token <token> | --email <email> --password <password>)
          nona migrate firebase [--config <path>] [--dry-run] [--base-url <url>] [--project <name>] [--token <token> | --email <email> --password <password>]

        Saved defaults:
          config file stores base-url and project for future commands

        On-prem:
          pass --base-url/--api-url, for example http://nona.internal:18080

        Environment variables:
          NONA_CLI_BASE_URL
          NONA_CLI_PROJECT_NAME
          NONA_CLI_BEARER_TOKEN
          NONA_CLI_EMAIL
          NONA_CLI_PASSWORD
        """;
}
