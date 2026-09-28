using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Nona.Cli.Tests.Entries;

internal sealed record NodeDotEnvCase(byte[] Bytes, Dictionary<string, string> Expected);

internal static class NodeDotEnvParser
{
    private const string Script = """
        import { parseEnv } from 'node:util';
        import assert from 'node:assert/strict';

        const major = Number(process.versions.node.split('.')[0]);
        assert.ok([22, 24].includes(major), `Tests require Node 22 or 24; found ${process.version}`);
        process.stdin.setEncoding('utf8');
        let input = '';
        for await (const chunk of process.stdin) input += chunk;
        const cases = JSON.parse(input);
        for (const [index, sample] of cases.entries()) {
          const actual = parseEnv(Buffer.from(sample.bytes, 'base64').toString('utf8'));
          const expectedKeys = Object.keys(sample.expected).sort();
          assert.deepEqual(Object.keys(actual).sort(), expectedKeys, `Case ${index}: own keys differ`);
          for (const key of expectedKeys) {
            assert.equal(actual[key], sample.expected[key], `Case ${index}: value differs for ${JSON.stringify(key)}`);
          }
        }
        process.stdout.write(JSON.stringify({ version: process.version, count: cases.length }));
        """;

    internal static async Task VerifyAsync(IReadOnlyList<NodeDotEnvCase> cases)
    {
        var start = new ProcessStartInfo("node")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("--input-type=module");
        start.ArgumentList.Add("--eval");
        start.ArgumentList.Add(Script);
        using var process = new Process { StartInfo = start };
        try { process.Start(); }
        catch (Win32Exception error)
        {
            throw new InvalidOperationException("Dotenv tests require Node.js 22 or 24 on PATH.", error);
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var readOutput = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var readError = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            try
            {
                await process.StandardInput.WriteAsync(JsonSerializer.Serialize(cases,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)).AsMemory(), timeout.Token);
                process.StandardInput.Close();
            }
            catch (IOException)
            {
                // A missing util.parseEnv export or unsupported Node version
                // can close the pipe early. Report Node's diagnostic below.
            }
            await process.WaitForExitAsync(timeout.Token);
            var output = await readOutput;
            var error = await readError;
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Node util.parseEnv verification failed: {error}");
            using var result = JsonDocument.Parse(output);
            if (result.RootElement.GetProperty("count").GetInt32() != cases.Count)
                throw new InvalidOperationException("Node did not verify the complete dotenv batch.");
        }
        catch (OperationCanceledException error)
        {
            throw new TimeoutException("Node dotenv verification exceeded 30 seconds.", error);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
    }
}
