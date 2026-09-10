#!/usr/bin/env python3
"""Install the sample and run its tests on one explicitly selected emulator."""
import argparse
import os
from pathlib import Path
import subprocess
import shlex
import sys
from urllib.parse import urlunsplit

sys.path.insert(0, str(Path(__file__).resolve().parent))
from adb_transport import configure_reverse

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'qa'))
from fixtures import load_fixtures, validate_loopback_url

parser = argparse.ArgumentParser()
parser.add_argument('--serial', required=True)
parser.add_argument('--fixtures', required=True)
parser.add_argument('--output', required=True)
args = parser.parse_args()
try:
    fixtures = load_fixtures(args.fixtures, require_admin=True)
    server = validate_loopback_url(fixtures['baseUrl'])
except ValueError as error:
    parser.error(str(error))
sdk = os.environ.get('ANDROID_HOME') or os.environ['ANDROID_SDK_ROOT']
adb = [str(Path(sdk) / 'platform-tools/adb'), '-s', args.serial]
root = Path(__file__).resolve().parents[1]
port = server.port or (443 if server.scheme == 'https' else 80)
configure_reverse(adb, [port, 18687, 18688])
for apk in ['sample/build/outputs/apk/debug/sample-debug.apk',
            'sample/build/outputs/apk/androidTest/debug/sample-debug-androidTest.apk']:
    subprocess.run(adb + ['install', '-r', str(root / apk)], check=True)
emulator_url = urlunsplit((server.scheme, f'127.0.0.1:{port}',
                          server.path, '', ''))
command = adb + ['shell', 'am', 'instrument', '-w', '-r', '-e', 'notAnnotation',
                 'com.nonaconfig.sample.ManualProbe', '-e', 'baseUrl', emulator_url,
                 '-e', 'faultBaseUrl', 'http://127.0.0.1:18687']
for name, value in {'frontendA': fixtures['sdk-qa-a'], 'frontendB': fixtures['sdk-qa-b'],
                    'backendKey': fixtures['backendKey'], 'adminToken': fixtures['adminToken']}.items():
    command += ['-e', name, value]
command += ['com.nonaconfig.sample.test/androidx.test.runner.AndroidJUnitRunner']
# adb shell reconstructs a command string; quote each remote argument separately.
command = command[:len(adb) + 1] + [shlex.quote(value) for value in command[len(adb) + 1:]]
result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=180)
output = Path(args.output)
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(result.stdout)
print(result.stdout)
if result.returncode or 'OK (' not in result.stdout or 'FAILURES!!!' in result.stdout:
    diagnostics = []
    for suffix in [['reverse', '--list'], ['shell', 'ip', 'route'],
                   ['shell', 'getprop', 'sys.boot_completed']]:
        try:
            diagnostic = subprocess.run(adb + suffix, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                        text=True, timeout=10)
            diagnostics.append(' '.join(suffix) + '\n' + diagnostic.stdout)
        except (OSError, subprocess.TimeoutExpired) as error:
            diagnostics.append(' '.join(suffix) + ': ' + type(error).__name__)
    output.with_suffix(output.suffix + '.network.txt').write_text('\n'.join(diagnostics))
    raise SystemExit(1)
