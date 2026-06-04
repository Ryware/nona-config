using System.Text;

namespace Nona.Libsql;

internal static class SqlScriptParser
{
    public static IReadOnlyList<string> SplitStatements(string script)
    {
        ArgumentNullException.ThrowIfNull(script);

        var statements = new List<string>();
        var current = new StringBuilder();
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var inLineComment = false;
        var inBlockComment = false;

        for (var index = 0; index < script.Length; index++)
        {
            var currentChar = script[index];
            var nextChar = index + 1 < script.Length ? script[index + 1] : '\0';

            if (inLineComment)
            {
                current.Append(currentChar);
                if (currentChar == '\n')
                {
                    inLineComment = false;
                }

                continue;
            }

            if (inBlockComment)
            {
                current.Append(currentChar);
                if (currentChar == '*' && nextChar == '/')
                {
                    current.Append(nextChar);
                    index++;
                    inBlockComment = false;
                }

                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && currentChar == '-' && nextChar == '-')
            {
                current.Append(currentChar);
                current.Append(nextChar);
                index++;
                inLineComment = true;
                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && currentChar == '/' && nextChar == '*')
            {
                current.Append(currentChar);
                current.Append(nextChar);
                index++;
                inBlockComment = true;
                continue;
            }

            if (currentChar == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                current.Append(currentChar);
                continue;
            }

            if (currentChar == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                current.Append(currentChar);
                continue;
            }

            if (currentChar == ';' && !inSingleQuote && !inDoubleQuote)
            {
                AddStatementIfPresent(statements, current);
                current.Clear();
                continue;
            }

            current.Append(currentChar);
        }

        AddStatementIfPresent(statements, current);
        return statements;
    }

    private static void AddStatementIfPresent(List<string> statements, StringBuilder builder)
    {
        var statement = builder.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(statement))
        {
            statements.Add(statement);
        }
    }
}
