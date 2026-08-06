#!/usr/bin/env node

import { readFile, writeFile } from "node:fs/promises";
import { relative, resolve } from "node:path";

const expectedBuilderPaths = [
  "cli/src/Nona.Cli/Core/Generated/Admin/AuditLogs/Export/ExportRequestBuilder.cs",
  "migrator/src/ConfigMigrator.Core/Generated/Admin/AuditLogs/Export/ExportRequestBuilder.cs",
];
const generatedAccept = 'requestInfo.Headers.TryAdd("Accept", "application/json");';
const normalizedAccept =
  'requestInfo.Headers.TryAdd("Accept", "text/csv, application/json");';

function count(source, value) {
  return source.split(value).length - 1;
}

function fail(message) {
  console.error(`Generated export Accept normalization failed: ${message}`);
  process.exitCode = 1;
}

const repositoryRoot = resolve(import.meta.dirname, "..");
const requestedPaths = process.argv.slice(2);

if (requestedPaths.length !== expectedBuilderPaths.length) {
  fail(
    `expected ${expectedBuilderPaths.length} generated builder paths, received ${requestedPaths.length}`,
  );
} else {
  const relativePaths = requestedPaths.map(file =>
    relative(repositoryRoot, resolve(file)).replaceAll("\\", "/"),
  );

  for (const expectedPath of expectedBuilderPaths) {
    if (!relativePaths.includes(expectedPath)) {
      fail(`expected builder path was not provided: ${expectedPath}`);
    }
  }

  if (process.exitCode !== 1) {
    const builders = [];

    for (const requestedPath of requestedPaths) {
      const file = resolve(requestedPath);
      const displayPath = relative(repositoryRoot, file).replaceAll("\\", "/");
      const source = await readFile(file, "utf8");
      const builderCount = count(
        source,
        "public partial class ExportRequestBuilder : BaseRequestBuilder",
      );
      const requestMethodCount = count(
        source,
        "public RequestInformation ToGetRequestInformation(",
      );
      const generatedAcceptCount = count(source, generatedAccept);
      const normalizedAcceptCount = count(source, normalizedAccept);

      if (
        builderCount !== 1 ||
        requestMethodCount !== 2 ||
        generatedAcceptCount !== 1 ||
        normalizedAcceptCount !== 0
      ) {
        fail(
          `${displayPath} did not match the expected Kiota shape ` +
            `(builder=${builderCount}, requestMethod=${requestMethodCount}, ` +
            `generatedAccept=${generatedAcceptCount}, normalizedAccept=${normalizedAcceptCount})`,
        );
        continue;
      }

      builders.push({ file, displayPath, source });
    }

    if (process.exitCode !== 1 && builders.length === expectedBuilderPaths.length) {
      for (const { file, displayPath, source } of builders) {
        const normalized = source.replace(generatedAccept, normalizedAccept);
        if (
          count(normalized, generatedAccept) !== 0 ||
          count(normalized, normalizedAccept) !== 1
        ) {
          fail(`${displayPath} could not be normalized exactly once`);
          continue;
        }

        await writeFile(file, normalized, "utf8");
        console.log(`  normalized Accept header: ${displayPath}`);
      }
    }
  }
}

if (process.exitCode === 1) {
  process.exit(1);
}
