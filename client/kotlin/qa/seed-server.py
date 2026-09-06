#!/usr/bin/env python3
"""Seed a disposable local Nona server; never use a production database."""
import argparse
import json
import secrets
import urllib.request
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--url', default='http://127.0.0.1:18686')
parser.add_argument('--output', required=True)
args = parser.parse_args()
if urllib.parse.urlparse(args.url).hostname not in ('127.0.0.1', 'localhost'):
    parser.error('Only a disposable loopback server is supported')
token = None

def api(method, path, body=None):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    req = urllib.request.Request(args.url + path, data=json.dumps(body).encode() if body is not None else None,
                                 headers=headers, method=method)
    with urllib.request.urlopen(req, timeout=15) as response:
        raw = response.read()
        return json.loads(raw) if raw else None

password = secrets.token_urlsafe(24) + '!1aA'
result = api('POST', '/auth/register', {'email': 'android-qa@example.test', 'password': password})
token = result['token']
fixtures = {'baseUrl': args.url, 'adminToken': token}
for project, value in [('android-qa-a', 'A'), ('android-qa-b', 'B')]:
    api('POST', '/admin/projects', {'name': project})
    prefix = f'/admin/projects/{project}/environments/Production'
    for key, val, content, scope in [('flag', value, 'text', 'client'), ('Features:Checkout', 'true', 'boolean', 'client'),
                                     ('Limits:Retries', 'bad', 'text', 'client'), ('Hidden', 'server-only', 'text', 'server')]:
        api('PUT', prefix + '/config-entries/' + urllib.parse.quote(key, safe=''),
            {'value': val, 'contentType': content, 'scope': scope})
    api('POST', prefix + '/releases', {'version': '1.0.0', 'makeActive': True})
    if project == 'android-qa-a':
        api('PUT', prefix + '/config-entries/flag', {'value': 'A-new', 'contentType': 'text', 'scope': 'client'})
        api('POST', prefix + '/releases', {'version': '2.0.0', 'makeActive': False})
    fixtures[project] = api('POST', f'/admin/projects/{project}/api-keys', {'name': 'Android QA', 'scope': 'client'})['key']
fixtures['backendKey'] = api('POST', '/admin/projects/android-qa-a/api-keys', {'name': 'Backend QA', 'scope': 'server'})['key']
path = Path(args.output)
path.write_text(json.dumps(fixtures))
path.chmod(0o600)
print('Seeded two projects, frontend/backend keys and releases. Fixtures: ' + str(path))
