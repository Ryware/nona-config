using System.Text;
using System.Text.Json;
using Nona.Cli.Entries;
using Nona.Cli.Generated.Models;
using static Nona.Cli.Tests.TestHelpers;

#pragma warning disable TUnit0055

namespace Nona.Cli.Tests.Entries;

[NotInParallel]
public sealed class DotEnvNodeCompatibilityTests
{
    private static readonly string?[] RegressionValues = [
        null, "", " ", "\t", "\n", "`hello`", "it's \"ok\"  ",
        "it's `ok`#", "it's `ok`\\r#", "'`#\\\\path", "a'\"`b",
        "line\none", "line'\none", "line'`\none", "line\nINJECTED=wrong\nlast",
        "a#b", "\\n\\r\\\\", "'\\n#", "`\\n#", "${VAR}",
        "trailing'", "trailing\"", "trailing`", "\u00a0hello\u00a0", "\u2028hello\u2029",
        "\0hello\0", "\ufeffhello", "Привет 😀", new string('x', 100_000) + "\n#tail"
    ];

    [Test]
    public async Task Format_RoundTripsRegressionValuesAndUnusualKeysThroughNode()
    {
        var cases = RegressionValues.Select(FormatCase).ToList();
        Dictionary<string, string> unusual = new(StringComparer.Ordinal)
        {
            ["Group.One"] = "dot",
            ["Group-One"] = "dash",
            ["123"] = "digits",
            ["Ångström"] = "Unicode",
            ["😀"] = "emoji",
            ["constructor"] = "ctor",
            ["toString"] = "method",
            ["__PROTO__"] = "upper",
            ["K#middle"] = "hash",
            ["\0K"] = "nul",
            ["\u00a0K\u00a0"] = "nbsp"
        };
        cases.Add(new(Encoding.UTF8.GetBytes(DotEnvEntryFormatter.Format(
            unusual.Select(entry => new ConfigEntryDto { Key = entry.Key, Value = entry.Value })) + "\n"), unusual));
        cases.Add(new([], new(StringComparer.Ordinal)));
        await NodeDotEnvParser.VerifyAsync(cases);
    }

    [Test]
    public async Task Format_RoundTripsGeneratedSupportedValues_AndRejectsUnsupportedValues()
    {
        // Each family has an independent acceptance guarantee: safe raw text,
        // a missing quote delimiter, or a known lossy input. This catches both
        // corruption and over-rejection without duplicating the encoder.
        var random = new Random(13094);
        string[] general = ["a", "b", "n", "r", "'", "\"", "`", "\\", "#", "\n", "\t", " ", "\u00a0", "\0", "😀", "Ж"];
        var raw = general.Where(c => c != "#" && c != "\n").ToArray();
        var single = general.Where(c => c != "'").ToArray();
        var backtick = general.Where(c => c != "`").ToArray();
        var doubleQuote = general.Where(c => c != "\"" && c != "n").ToArray();
        var cases = new List<NodeDotEnvCase>(12_000);
        var rejected = 0;
        for (var i = 0; i < 20_000; i++)
        {
            var family = i % 10;
            var value = family switch
            {
                0 => "a" + Generate(raw) + "z",
                1 => Generate(single),
                2 => "'" + Generate(backtick) + "\t",
                3 => "'`#" + Generate(doubleQuote),
                4 => "😀" + Generate(single) + "\u00a0",
                5 => "\n" + Generate(backtick) + "'",
                6 => Generate(general) + "\r",
                7 => Generate(general) + (i % 20 == 7 ? "\ud800" : "\udfff"),
                8 => "a'\"`#" + Generate(general),
                _ => "a'`\\n#" + Generate(backtick)
            };
            if (family < 6) cases.Add(FormatCase(value));
            else
            {
                var threw = false;
                try { FormatCase(value); }
                catch (DotEnvExportValidationException) { threw = true; }
                await Assert.That(threw).IsTrue();
                rejected++;
            }
        }
        await Assert.That(cases.Count).IsEqualTo(12_000);
        await Assert.That(rejected).IsEqualTo(8_000);
        await NodeDotEnvParser.VerifyAsync(cases);

        string Generate(string[] alphabet)
        {
            var result = new StringBuilder();
            var length = random.Next(0, 41);
            for (var i = 0; i < length; i++) result.Append(alphabet[random.Next(alphabet.Length)]);
            return result.ToString();
        }
    }

    [Test]
    [Arguments("working")]
    [Arguments("exact")]
    [Arguments("active")]
    public async Task Export_FileBytesAndStdoutRoundTripThroughNode(string source)
    {
        var entries = RegressionValues.Select((value, i) => new
        {
            key = $"Group:Value{i:D2}",
            value,
            contentType = "text",
            scope = "all"
        }).ToArray();
        var json = JsonSerializer.Serialize(entries);
        var expected = entries.ToDictionary(e => e.key.Replace(":", "__"), e => e.value ?? "", StringComparer.Ordinal);
        var (stdoutCode, stdout, stdoutError) = await ExportEntriesValidationTests.ExportAsync(source, json);
        using var file = new TempFile();
        var (fileCode, _, fileError) = await ExportEntriesValidationTests.ExportAsync(source, json, file.Path);
        await Assert.That(stdoutCode).IsEqualTo(0);
        await Assert.That(fileCode).IsEqualTo(0);
        await Assert.That(stdoutError).IsEmpty();
        await Assert.That(fileError).IsEmpty();
        var bytes = await File.ReadAllBytesAsync(file.Path);
        await Assert.That(bytes.SequenceEqual(Encoding.UTF8.GetBytes(stdout))).IsTrue();
        await Assert.That(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF })).IsFalse();
        await Assert.That(bytes[^1]).IsEqualTo((byte)'\n');
        await NodeDotEnvParser.VerifyAsync([new(bytes, expected), new(Encoding.UTF8.GetBytes(stdout), expected)]);
    }

    [Test]
    public async Task Export_ProcessStdoutAndFileBytesRoundTripThroughNode()
    {
        var entries = JsonSerializer.Serialize(new[] {
            new { key = "Group:Unicode", value = "Привет 😀", contentType = "text", scope = "all" },
            new { key = "Quoted", value = "it's \"ok\"#\\n", contentType = "text", scope = "all" },
            new { key = "Multi", value = "\nINJECTED=wrong\n'`tail", contentType = "text", scope = "all" }
        });
        var (code, output, error) = await CliExportProcess.RunAsync(entries);
        using var file = new TempFile();
        var (fileCode, _, fileError) = await CliExportProcess.RunAsync(entries, file.Path);
        await Assert.That(code).IsEqualTo(0);
        await Assert.That(fileCode).IsEqualTo(0);
        await Assert.That(error).IsEmpty();
        await Assert.That(fileError).IsEmpty();
        var bytes = await File.ReadAllBytesAsync(file.Path);
        await Assert.That(bytes.SequenceEqual(output)).IsTrue();
        Dictionary<string, string> expected = new(StringComparer.Ordinal)
        {
            ["Group__Unicode"] = "Привет 😀",
            ["Quoted"] = "it's \"ok\"#\\n",
            ["Multi"] = "\nINJECTED=wrong\n'`tail"
        };
        await NodeDotEnvParser.VerifyAsync([new(output, expected), new(bytes, expected)]);
    }

    private static NodeDotEnvCase FormatCase(string? value)
    {
        var text = DotEnvEntryFormatter.Format([
            new() { Key = "A:Before", Value = "before" },
            new() { Key = "K:Value", Value = value },
            new() { Key = "Z:After", Value = "after" }
        ]) + "\n";
        return new(Encoding.UTF8.GetBytes(text), new(StringComparer.Ordinal)
        {
            ["A__Before"] = "before",
            ["K__Value"] = value ?? "",
            ["Z__After"] = "after"
        });
    }
}
