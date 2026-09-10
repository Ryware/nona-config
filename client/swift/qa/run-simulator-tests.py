#!/usr/bin/env python3
"""Run Swift unit and integration tests on an explicitly selected iOS simulator."""
import argparse
import os
from pathlib import Path
import subprocess
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'qa'))
from fixtures import load_fixtures, validate_loopback_url

parser = argparse.ArgumentParser()
parser.add_argument('--simulator', required=True)
parser.add_argument('--fixtures', required=True)
parser.add_argument('--result-bundle', required=True)
parser.add_argument('--fault-url', default='http://127.0.0.1:18687')
args = parser.parse_args()
try:
    fixtures = load_fixtures(args.fixtures)
    validate_loopback_url(args.fault_url)
except ValueError as error:
    parser.error(str(error))
environment = os.environ.copy()
for name, value in {
    'NONA_BASE_URL': fixtures['baseUrl'], 'NONA_FRONTEND_A': fixtures['sdk-qa-a'],
    'NONA_FRONTEND_B': fixtures['sdk-qa-b'], 'NONA_BACKEND_KEY': fixtures['backendKey'],
    'NONA_FAULT_URL': args.fault_url,
}.items():
    environment['TEST_RUNNER_' + name] = value
project = Path(__file__).resolve().parents[1] / 'Sample/NonaSample.xcodeproj'
subprocess.run(['xcodebuild', '-project', str(project), '-scheme', 'NonaSample',
                '-destination', 'platform=iOS Simulator,id=' + args.simulator,
                '-resultBundlePath', args.result_bundle, '-parallel-testing-enabled', 'NO',
                'CODE_SIGNING_ALLOWED=NO', 'SWIFT_TREAT_WARNINGS_AS_ERRORS=YES', 'test'], env=environment, check=True)
