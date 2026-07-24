import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const backendRoot = resolve(repoRoot, process.env.NONA_BACKEND_DIR ?? '..');
const cliProject = resolve(backendRoot, 'cli/src/Nona.Cli/Nona.Cli.csproj');
const cliDll = resolve(backendRoot, 'cli/src/Nona.Cli/bin/Debug/net10.0/Nona.Cli.dll');
const outputFile = resolve(repoRoot, 'src/content/docs/cli/reference.md');

const seen = new Set();
const pages = [];
const commonOptionDefinitions = [
	{
		key: 'baseUrl',
		label: '`--api-url, --base-url <base-url>`',
		pattern: /^\s+(?:--api-url,\s+--base-url|--base-url,\s+--api-url) <base-url>\s+Nona API base URL\.\s*$/,
	},
	{
		key: 'token',
		label: '`--bearer-token, --token <bearer-token>`',
		pattern: /^\s+(?:--bearer-token,\s+--token|--token,\s+--bearer-token) <bearer-token>\s+Bearer token\.\s*$/,
	},
	{
		key: 'help',
		label: '`-?, -h, --help`',
		pattern: /^\s+-\?,\s+-h,\s+--help\s+Show help and usage information\s*$/,
	},
	{
		key: 'verbose',
		label: '`--verbose`',
		pattern: /^\s+--verbose\s+Show full exception details when a command fails\.\s*$/,
	},
];

execFileSync('dotnet', ['build', cliProject, '--nologo', '--verbosity', 'quiet'], {
	stdio: 'inherit',
});

if (!existsSync(cliDll)) {
	throw new Error(`Expected built CLI at ${cliDll}`);
}

walk([]);
writeReference();

function walk(path) {
	const key = path.join(' ');
	if (seen.has(key)) return;
	seen.add(key);

	const help = readHelp(path);
	const normalized = removeRepeatedCommonOptions(help);
	pages.push({ path, help: normalized.help, commonOptions: normalized.commonOptions });

	for (const command of parseChildCommands(help)) {
		walk([...path, command]);
	}
}

function readHelp(path) {
	const output = execFileSync(
		'dotnet',
		[cliDll, ...path, '--help'],
		{ encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'] },
	);

	return output.replaceAll('Nona.Cli', 'nona').trim();
}

function parseChildCommands(help) {
	const lines = help.split(/\r?\n/);
	const commands = [];
	let inCommands = false;

	for (const line of lines) {
		if (line.trim() === 'Commands:') {
			inCommands = true;
			continue;
		}

		if (!inCommands) continue;
		if (!line.trim()) continue;
		if (!line.startsWith(' ')) break;

		const match = line.match(/^\s+(.+?)(?:\s{2,}|\t+).+$/);
		if (!match) continue;

		const primary = match[1].split(',')[0]?.trim().split(/\s+/)[0];
		if (primary && !primary.startsWith('-')) {
			commands.push(primary);
		}
	}

	return commands;
}

function removeRepeatedCommonOptions(help) {
	const lines = help.split(/\r?\n/);
	const output = [];
	const commonOptions = new Set();

	for (let index = 0; index < lines.length;) {
		const line = lines[index];
		if (line.trim() !== 'Options:') {
			output.push(line);
			index++;
			continue;
		}

		const header = line;
		index++;
		const optionLines = [];

		while (index < lines.length) {
			const optionLine = lines[index];
			if (optionLine.trim() === '') {
				optionLines.push(optionLine);
				index++;
				continue;
			}

			if (!optionLine.startsWith(' ')) break;

			const commonOption = matchCommonOption(optionLine);
			if (commonOption) {
				commonOptions.add(commonOption.key);
				index++;
				continue;
			}

			optionLines.push(optionLine);
			index++;
		}

		if (optionLines.some((optionLine) => optionLine.trim())) {
			output.push(header, ...optionLines);
		}
	}

	return {
		help: output.join('\n').trim(),
		commonOptions: commonOptionDefinitions
			.filter(({ key }) => commonOptions.has(key))
			.map(({ key }) => key),
	};
}

function matchCommonOption(line) {
	return commonOptionDefinitions.find(({ pattern }) => pattern.test(line));
}

function writeReference() {
	const lines = [
		'---',
		'title: CLI reference',
		'description: Generated command reference for the nona CLI, covering every command, flag, and option for managing config and migrations.',
		'---',
		'',
		'Generated from the `nona` command help output.',
		'',
		'Regenerate this page with:',
		'',
		'```bash',
		'npm run generate:cli',
		'```',
		'',
		'## Common options',
		'',
		'The command help below omits repeated common options from individual option lists.',
		'',
		'- `-?, -h, --help` is accepted by every command and subcommand.',
		'- `--verbose` includes the full exception and stack trace when a command fails. Without it, stack traces are suppressed.',
		'- Commands that connect to the Nona API may also accept `--api-url, --base-url <base-url>` and `--bearer-token, --token <bearer-token>`.',
		'- Connection values can come from flags, `NONA_CLI_*` environment variables, saved defaults, or a matching `nona auth login` session.',
		'',
		'## HTTP/API error output and exit codes',
		'',
		'HTTP/API failures are written to standard error as one human-readable line, including the HTTP status and the server\'s error code when available:',
		'',
		'```text',
		'Error: value is not a valid number (400, INVALID_VALUE)',
		'```',
		'',
		'| Exit code | HTTP/API failure |',
		'| --- | --- |',
		'| `2` | Validation or other client request error (`400`, `422`, or another `4xx`) |',
		'| `3` | Authentication or authorization error (`401` or `403`) |',
		'| `4` | Resource not found (`404`) |',
		'| `5` | Conflict (`409`) |',
		'| `6` | Server error (`5xx`) |',
		'',
		'Other command-specific failures may use different non-zero exit codes.',
		'',
	];

	for (const page of pages) {
		const command = page.path.length ? `nona ${page.path.join(' ')}` : 'nona';
		const sections = parseHelpSections(page.help);
		lines.push(`## \`${command}\``, '');
		if (sections.description) {
			lines.push(sections.description, '');
		}
		if (sections.usage) {
			lines.push('**Usage**', '', '```text', sections.usage, '```', '');
		}
		if (sections.arguments) {
			lines.push('**Arguments**', '', '```text', sections.arguments, '```', '');
		}
		if (sections.commands.length > 0) {
			lines.push('**Commands**', '');
			for (const item of sections.commands) {
				lines.push(`- \`${item.name}\` ${item.description}`);
			}
			lines.push('');
		}
		if (sections.options) {
			lines.push('**Options**', '', '```text', sections.options, '```', '');
		}
		const commandCommonOptions = page.commonOptions.filter(
			(option) => option !== 'help' && option !== 'verbose',
		);
		if (commandCommonOptions.length > 0) {
			lines.push(
				`Also accepts: ${commandCommonOptions.map(formatCommonOption).join(', ')}.`,
				'',
			);
		}
	}

	mkdirSync(dirname(outputFile), { recursive: true });
	writeFileSync(outputFile, `${lines.join('\n').trimEnd()}\n`);
}

function formatCommonOption(option) {
	return commonOptionDefinitions.find(({ key }) => key === option)?.label ?? option;
}

function parseHelpSections(help) {
	const lines = help.split(/\r?\n/);
	const sections = {
		description: [],
		usage: [],
		arguments: [],
		options: [],
		commands: [],
	};

	let current = null;

	for (const line of lines) {
		const trimmed = line.trim();
		switch (trimmed) {
			case 'Description:':
				current = 'description';
				continue;
			case 'Usage:':
				current = 'usage';
				continue;
			case 'Arguments:':
				current = 'arguments';
				continue;
			case 'Options:':
				current = 'options';
				continue;
			case 'Commands:':
				current = 'commands';
				continue;
		}

		if (current === null) continue;

		if (trimmed === '') {
			if (current === 'description' && sections.description.at(-1) !== '') {
				sections.description.push('');
			}
			continue;
		}

		sections[current].push(stripLeadingIndent(line));
	}

	return {
		description: collapseDescription(sections.description),
		usage: collapseBlock(sections.usage),
		arguments: collapseBlock(sections.arguments),
		options: collapseBlock(sections.options),
		commands: parseCommandRows(sections.commands),
	};
}

function stripLeadingIndent(line) {
	return line.replace(/^\s{2}/, '');
}

function collapseDescription(lines) {
	return lines
		.join('\n')
		.replace(/\n+/g, ' ')
		.replace(/\s+/g, ' ')
		.trim();
}

function collapseBlock(lines) {
	return lines.join('\n').trim();
}

function parseCommandRows(lines) {
	return lines
		.map((line) => line.match(/^(.+?)(?:\s{2,}|\t+)(.+)$/))
		.filter(Boolean)
		.map(([, name, description]) => ({
			name: name.trim(),
			description: description.trim(),
		}));
}
