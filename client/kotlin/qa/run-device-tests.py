#!/usr/bin/env python3
"""Install the sample and run its tests on one explicitly selected emulator."""
import argparse
import json
import os
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('--serial', required=True)
parser.add_argument('--fixtures', required=True)
parser.add_argument('--output', required=True)
args = parser.parse_args()
sdk = os.environ.get('ANDROID_HOME') or os.environ['ANDROID_SDK_ROOT']
adb = [str(Path(sdk) / 'platform-tools/adb'), '-s', args.serial]
root = Path(__file__).resolve().parents[1]
for apk in ['sample/build/outputs/apk/debug/sample-debug.apk',
            'sample/build/outputs/apk/androidTest/debug/sample-debug-androidTest.apk']:
    subprocess.run(adb + ['install', '-r', str(root / apk)], check=True)
fixtures = json.loads(Path(args.fixtures).read_text())
command = adb + ['shell', 'am', 'instrument', '-w', '-r']
for name, value in {'frontendA': fixtures['android-qa-a'], 'frontendB': fixtures['android-qa-b'],
                    'backendKey': fixtures['backendKey'], 'adminToken': fixtures['adminToken']}.items():
    command += ['-e', name, value]
command += ['com.nonaconfig.sample.test/androidx.test.runner.AndroidJUnitRunner']
result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=180)
output = Path(args.output)
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(result.stdout)
print(result.stdout)
if result.returncode or 'OK (' not in result.stdout or 'FAILURES!!!' in result.stdout:
    raise SystemExit(1)
