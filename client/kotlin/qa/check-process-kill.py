#!/usr/bin/env python3
"""Kill the owned QA instrumentation during SDK writes, then validate its private cache."""
import argparse
import json
import os
from pathlib import Path
import shlex
import subprocess
import sys
import time
from urllib.parse import urlsplit, urlunsplit

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'qa'))
from fixtures import load_fixtures

parser = argparse.ArgumentParser()
parser.add_argument('--serial', required=True)
parser.add_argument('--fixtures', required=True)
parser.add_argument('--output', required=True)
args = parser.parse_args()
f = load_fixtures(args.fixtures)
u = urlsplit(f['baseUrl'])
url = urlunsplit((u.scheme, '10.0.2.2' + (':' + str(u.port) if u.port else ''), u.path, '', ''))
sdk = os.environ.get('ANDROID_HOME') or os.environ['ANDROID_SDK_ROOT']
adb = [str(Path(sdk) / 'platform-tools/adb'), '-s', args.serial]
package = 'com.nonaconfig.sample'

def shell(*command, check=True):
    return subprocess.run(adb + ['shell'] + [shlex.quote(p) for p in command], text=True,
                          stdout=subprocess.PIPE, stderr=subprocess.STDOUT, check=check, timeout=10)

# Only a disposable emulator is supported. Never force-stop a physical user's app.
if shell('getprop', 'ro.kernel.qemu').stdout.strip() != '1':
    raise SystemExit('Select an Android emulator')
shell('am', 'force-stop', package)
shell('run-as', package, 'rm', '-rf', 'files/process-kill-probe')
command = ['am', 'instrument', '-w', '-r', '-e', 'class',
           'com.nonaconfig.sample.NonaDeviceTest#processKillProbe', '-e', 'crashProbe', 'true',
           '-e', 'baseUrl', url, '-e', 'frontendA', f['sdk-qa-a'],
           'com.nonaconfig.sample.test/androidx.test.runner.AndroidJUnitRunner']
output = Path(args.output)
output.parent.mkdir(parents=True, exist_ok=True)
with output.open('w') as log:
    child = subprocess.Popen(adb + ['shell'] + [shlex.quote(p) for p in command], stdout=log, stderr=subprocess.STDOUT)
    try:
        deadline = time.monotonic() + 20
        while shell('run-as', package, 'cat', 'files/process-kill-probe/ready', check=False).returncode:
            if child.poll() is not None or time.monotonic() > deadline:
                raise RuntimeError('Writer did not become ready; inspect the probe log')
            time.sleep(0.05)
        time.sleep(0.025)
        shell('am', 'force-stop', package)
        child.wait(timeout=10)
    finally:
        shell('am', 'force-stop', package)
        if child.poll() is None:
            child.kill()
            child.wait(timeout=5)
files = shell('run-as', package, 'ls', 'files/process-kill-probe/nona').stdout.splitlines()
caches = [name for name in files if name.startswith('snapshot-') and name.endswith('.json')]
assert len(caches) == 1, 'Expected one complete cache file'
raw = shell('run-as', package, 'cat', 'files/process-kill-probe/nona/' + caches[0]).stdout
snapshot = json.loads(raw)
assert snapshot['values']['flag']['value'] == 'A', 'Cache contents changed or were corrupted'
print('PASS: Android process kill retained a complete SDK snapshot')
