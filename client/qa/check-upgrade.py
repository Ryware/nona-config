#!/usr/bin/env python3
"""Build an explicit local baseline and current sources; test cache upgrade and Swift process kill.
No tags, branches, publications or user checkout files are changed.
"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import time

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--baseline-ref', required=True)
parser.add_argument('--output', required=True, help='New directory for isolated builds and evidence')
args = parser.parse_args()
revision = subprocess.check_output(['git', 'rev-parse', '--verify', args.baseline_ref + '^{commit}'], cwd=root, text=True).strip()
out = Path(args.output).resolve()
out.mkdir(parents=True, exist_ok=False)
baseline = out / 'baseline'
baseline.mkdir()
archive = out / 'baseline.tar'
with archive.open('wb') as target:
    subprocess.run(['git', 'archive', revision], cwd=root, stdout=target, check=True)
subprocess.run(['tar', '-xf', str(archive), '-C', str(baseline)], check=True)
archive.unlink()
shutil.copytree(root / 'client/qa/consumers/swift', baseline / 'client/qa/consumers/swift', ignore=shutil.ignore_patterns('.build', '.swiftpm'))

def run(command, label, cwd=root, env=None):
    with (out / (label + '.log')).open('w') as log:
        subprocess.run(command, cwd=cwd, env=env, stdout=log, stderr=subprocess.STDOUT, check=True, timeout=300)
    print('PASS:', label, flush=True)

executables = {}
for name, source in [('baseline', baseline), ('current', root)]:
    package = source / 'client/qa/consumers/swift'
    scratch = out / ('swift-' + name)
    command = ['swift', 'build', '--package-path', str(package), '--scratch-path', str(scratch)]
    run(command, name + '-swift-build')
    binary = subprocess.check_output(command + ['--show-bin-path'], text=True).strip()
    executables[name] = str(Path(binary) / 'Probe')
cache = out / 'swift-cache.json'
for writer, reader in [('baseline', 'current'), ('current', 'baseline')]:
    run([executables[writer], 'seed', str(cache)], writer + '-swift-seed')
    run([executables[reader], 'read', str(cache)], reader + '-swift-read')

# Kill only the child we own, during repeated atomic writes of large snapshots.
ready = cache.with_suffix(cache.suffix + '.ready')
with (out / 'swift-kill.log').open('w') as log:
    child = subprocess.Popen([executables['current'], 'stress', str(cache)], stdout=log, stderr=subprocess.STDOUT)
    try:
        deadline = time.monotonic() + 10
        while not ready.exists():
            if child.poll() is not None or time.monotonic() > deadline:
                raise RuntimeError('Stress writer did not become ready')
            time.sleep(0.01)
        time.sleep(0.025)
        child.kill()
        child.wait(timeout=5)
    finally:
        if child.poll() is None:
            child.kill()
            child.wait(timeout=5)
value = json.loads(cache.read_text())['values']['flag']['value']
assert value in ('A' * 262144, 'B' * 262144), 'Process kill exposed a partial snapshot'
print('PASS: swift-process-kill', flush=True)

init = out / 'probe.gradle'
init.write_text('''allprojects {
    plugins.withId('com.android.library') {
        android.sourceSets.getByName('test').java.srcDir(System.getenv('NONA_COMPAT_SOURCE'))
    }
    tasks.withType(Test).configureEach {
        environment 'NONA_COMPAT_CACHE', System.getenv('NONA_COMPAT_CACHE')
        environment 'NONA_COMPAT_MODE', System.getenv('NONA_COMPAT_MODE')
        outputs.upToDateWhen { false }
    }
}
''')
kotlin_cache = out / 'kotlin-cache.json'
for name, source, mode in [('baseline', baseline, 'seed'), ('current', root, 'read'), ('current', root, 'seed'), ('baseline', baseline, 'read')]:
    environment = os.environ | {'NONA_COMPAT_SOURCE': str(root / 'client/qa/consumers/kotlin'),
                               'NONA_COMPAT_CACHE': str(kotlin_cache), 'NONA_COMPAT_MODE': mode}
    run([str(source / 'client/kotlin/gradlew'), '-p', str(source / 'client/kotlin'), '-I', str(init),
         ':nona-client:testDebugUnitTest', '--tests', 'com.nonaconfig.compat.CacheProbeTest', '--console=plain'],
        name + '-kotlin-' + mode, env=environment)
(out / 'summary.json').write_text(json.dumps({'baseline': revision, 'cacheUpgrade': 'passed-both-directions',
    'swiftProcessKill': 'passed', 'binaryCompatibility': 'not-implied-by-source-build'}, indent=2) + '\n')
print('Evidence:', out, flush=True)
