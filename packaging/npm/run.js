#!/usr/bin/env node
'use strict';

const { spawnSync } = require('child_process');
const path = require('path');
const fs = require('fs');
const { version } = require('./package.json');

const binaryName = process.platform === 'win32' ? 'nona.exe' : 'nona';
const binaryPath = path.join(__dirname, 'bin', binaryName);

if (process.argv.length === 3 && (process.argv[2] === '--version' || process.argv[2] === '-v')) {
  process.stdout.write(`${version}\n`);
  process.exit(0);
}

if (!fs.existsSync(binaryPath)) {
  console.error('nona binary not found. Try reinstalling: npm install -g nona-cli');
  process.exit(1);
}

const result = spawnSync(binaryPath, process.argv.slice(2), {
  stdio: 'inherit',
  env: {
    ...process.env,
    NONA_CLI_VERSION: process.env.NONA_CLI_VERSION || version,
  },
});

process.exit(result.status ?? 1);
