#!/usr/bin/env python3
"""Report removed public JVM entry points. Exit 1 is a release compatibility gate."""
import argparse
import os
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('--baseline-jar', required=True)
parser.add_argument('--current-jar', required=True)
args = parser.parse_args()
javap = str(Path(os.environ['JAVA_HOME']) / 'bin/javap') if 'JAVA_HOME' in os.environ else 'javap'
classes = ['NonaConfig', 'NonaConfig$Companion', 'NonaOptions', 'NonaOptions$Builder',
           'NonaOptions$Companion', 'NonaHttpClient', 'NonaHttpResponse', 'NonaSnapshotStore',
           'InMemorySnapshotStore', 'NonaEntry', 'NonaException', 'NonaHttpException',
           'NonaResolution$Success', 'NonaResolution$Failure']

def api(jar):
    result = set()
    for name in classes:
        output = subprocess.check_output([javap, '-public', '-s', '-classpath', jar, 'com.nonaconfig.client.' + name], text=True)
        previous = ''
        for line in output.splitlines():
            line = line.strip()
            if line.startswith('descriptor:') and 'access$' not in previous:
                result.add(name + ' ' + previous + ' ' + line)
            previous = line
    return result

old, new = api(args.baseline_jar), api(args.current_jar)
removed = sorted(old - new)
if removed:
    print('BINARY API CHANGES — migrate consumers and choose an appropriate new version:')
    print('\n'.join(removed))
    raise SystemExit(1)
print('PASS: existing public JVM signatures retained')
