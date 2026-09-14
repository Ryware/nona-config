"""Idempotent, explicit ADB reverse mappings for the disposable QA endpoints."""
import subprocess


def configure_reverse(adb, ports):
    result = subprocess.run(adb + ['reverse', '--list'], check=True,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=10)
    existing = {}
    for line in result.stdout.splitlines():
        fields = line.split()
        if len(fields) >= 3:
            existing[fields[-2]] = fields[-1]
    for port in sorted(set(ports)):
        endpoint = f'tcp:{port}'
        if endpoint in existing:
            if existing[endpoint] != endpoint:
                raise RuntimeError(f'Conflicting ADB reverse mapping for {endpoint}; select a dedicated QA emulator')
            continue
        subprocess.run(adb + ['reverse', '--no-rebind', endpoint, endpoint], check=True, timeout=10)
