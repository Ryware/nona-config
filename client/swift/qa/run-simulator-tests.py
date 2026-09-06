#!/usr/bin/env python3
"""Run Swift unit and integration tests on an explicitly selected iOS simulator."""
import argparse
import json
import os
from pathlib import Path
import subprocess
from urllib.parse import urlsplit

parser = argparse.ArgumentParser()
parser.add_argument('--simulator', required=True)
parser.add_argument('--fixtures', required=True)
parser.add_argument('--result-bundle', required=True)
parser.add_argument('--fault-url', default='http://127.0.0.1:18687')
args = parser.parse_args()
fixtures = json.loads(Path(args.fixtures).read_text())
for url in [fixtures['baseUrl'], args.fault_url]:
    parsed = urlsplit(url)
    if parsed.hostname not in ('127.0.0.1', 'localhost') or parsed.scheme not in ('http', 'https'):
        parser.error('Only disposable loopback servers are supported')
environment = os.environ.copy()
for name, value in {
    'NONA_BASE_URL': fixtures['baseUrl'], 'NONA_FRONTEND_A': fixtures['android-qa-a'],
    'NONA_FRONTEND_B': fixtures['android-qa-b'], 'NONA_BACKEND_KEY': fixtures['backendKey'],
    'NONA_FAULT_URL': args.fault_url,
}.items():
    environment['TEST_RUNNER_' + name] = value
project = Path(__file__).resolve().parents[1] / 'Sample/NonaSample.xcodeproj'
subprocess.run(['xcodebuild', '-project', str(project), '-scheme', 'NonaSample',
                '-destination', 'platform=iOS Simulator,id=' + args.simulator,
                '-resultBundlePath', args.result_bundle, '-parallel-testing-enabled', 'NO',
                'CODE_SIGNING_ALLOWED=NO', 'SWIFT_TREAT_WARNINGS_AS_ERRORS=YES', 'test'], env=environment, check=True)
