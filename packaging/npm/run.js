#!/usr/bin/env node
'use strict';

const { spawnSync } = require('child_process');
const path = require('path');
const fs = require('fs');

const binaryName = process.platform === 'win32' ? 'nona.exe' : 'nona';
const binaryPath = path.join(__dirname, 'bin', binaryName);

if (!fs.existsSync(binaryPath)) {
  console.error('nona binary not found. Try reinstalling: npm install -g nona-cli');
  process.exit(1);
}

const result = spawnSync(binaryPath, process.argv.slice(2), {
  stdio: 'inherit',
  env: process.env,
});

process.exit(result.status ?? 1);
