'use strict';

const https = require('https');
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

const { version, repository } = require('./package.json');
const repoUrl = repository.url.replace(/\.git$/, '');
const repoPath = repoUrl.replace('https://github.com/', '');

const TARGETS = {
  'win32-x64':    { rid: 'win-x64',    ext: 'zip',    bin: 'nona.exe' },
  'win32-arm64':  { rid: 'win-arm64',  ext: 'zip',    bin: 'nona.exe' },
  'linux-x64':    { rid: 'linux-x64',  ext: 'tar.gz', bin: 'nona'     },
  'linux-arm64':  { rid: 'linux-arm64',ext: 'tar.gz', bin: 'nona'     },
  'darwin-x64':   { rid: 'osx-x64',   ext: 'tar.gz', bin: 'nona'     },
  'darwin-arm64': { rid: 'osx-arm64', ext: 'tar.gz', bin: 'nona'     },
};

const key = `${process.platform}-${process.arch}`;
const target = TARGETS[key];

if (!target) {
  console.error(`nona-cli: unsupported platform ${key}.`);
  console.error(`Install manually from https://github.com/${repoPath}/releases`);
  process.exit(1);
}

const { rid, ext, bin } = target;
const assetName = `nona-cli_${version}_${rid}.${ext}`;
const downloadUrl = `https://github.com/${repoPath}/releases/download/cli-v${version}/${assetName}`;
const binDir = path.join(__dirname, 'bin');
const archivePath = path.join(binDir, assetName);

fs.mkdirSync(binDir, { recursive: true });

function download(url, dest, cb) {
  const file = fs.createWriteStream(dest);
  https.get(url, (res) => {
    if (res.statusCode === 301 || res.statusCode === 302) {
      file.close();
      fs.unlink(dest, () => download(res.headers.location, dest, cb));
      return;
    }
    if (res.statusCode !== 200) {
      file.close();
      fs.unlink(dest, () => {});
      cb(new Error(`HTTP ${res.statusCode} downloading ${url}`));
      return;
    }
    res.pipe(file);
    file.on('finish', () => file.close(cb));
  }).on('error', (err) => {
    fs.unlink(dest, () => {});
    cb(err);
  });
}

process.stdout.write(`Downloading nona-cli ${version} for ${key}...\n`);

download(downloadUrl, archivePath, (err) => {
  if (err) {
    console.error(`nona-cli install failed: ${err.message}`);
    process.exit(1);
  }

  try {
    if (ext === 'zip') {
      execSync(
        `powershell -Command "Expand-Archive -Path '${archivePath}' -DestinationPath '${binDir}' -Force"`,
        { stdio: 'pipe' }
      );
    } else {
      execSync(`tar -xzf "${archivePath}" -C "${binDir}"`, { stdio: 'pipe' });
    }

    fs.unlinkSync(archivePath);

    const binaryPath = path.join(binDir, bin);
    if (process.platform !== 'win32') {
      fs.chmodSync(binaryPath, 0o755);
    }

    process.stdout.write(`nona-cli ready at ${binaryPath}\n`);
  } catch (e) {
    console.error(`nona-cli extraction failed: ${e.message}`);
    process.exit(1);
  }
});
