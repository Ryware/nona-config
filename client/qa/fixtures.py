"""Shared fixture validation; runs before any simulator/device side effects."""
import json
from pathlib import Path
import re
from urllib.parse import urlsplit


def validate_loopback_url(url):
    try:
        parsed = urlsplit(url)
        valid = (parsed.scheme in ('http', 'https')
                 and parsed.hostname in ('127.0.0.1', 'localhost')
                 and parsed.username is None and parsed.password is None
                 and not parsed.query and not parsed.fragment
                 and (parsed.port is None or parsed.port > 0))
    except (ValueError, TypeError, AttributeError):
        valid = False
    if not valid:
        raise ValueError('QA URLs must use HTTP(S) loopback without credentials, query or fragment')
    return parsed


def load_fixtures(path, *, require_admin=False):
    try:
        fixtures = json.loads(Path(path).read_text())
    except (OSError, ValueError):
        raise ValueError('Cannot read QA fixtures; run client/qa/seed-server.py first') from None
    if not isinstance(fixtures, dict):
        raise ValueError('QA fixtures must be a JSON object')
    validate_loopback_url(fixtures.get('baseUrl'))
    required = ['sdk-qa-a', 'sdk-qa-b', 'backendKey']
    if require_admin:
        required.append('adminToken')
    for name in required:
        value = fixtures.get(name)
        if not isinstance(value, str) or not re.fullmatch(r'[A-Za-z0-9._-]+', value):
            raise ValueError(f'Invalid or missing {name}; regenerate fixtures with client/qa/seed-server.py')
    return fixtures
