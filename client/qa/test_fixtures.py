import contextlib
import io
import json
from pathlib import Path
import runpy
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

from fixtures import load_fixtures, validate_loopback_url


class FixtureTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.path = Path(self.directory.name) / 'fixtures.json'
        self.data = {'baseUrl': 'http://127.0.0.1:18786', 'sdk-qa-a': 'test-a',
                     'sdk-qa-b': 'test-b', 'backendKey': 'test-backend', 'adminToken': 'test-admin'}
        self.path.write_text(json.dumps(self.data))

    def test_current_fixtures(self):
        self.assertEqual(load_fixtures(self.path, require_admin=True), self.data)

    def test_rejects_missing_or_legacy_keys(self):
        del self.data['sdk-qa-a']
        self.data['android-qa-a'] = 'legacy'
        self.path.write_text(json.dumps(self.data))
        with self.assertRaisesRegex(ValueError, 'regenerate fixtures'):
            load_fixtures(self.path)

    def test_rejects_malformed_and_unreadable_files(self):
        for value in ['[]', 'null', 'invalid']:
            self.path.write_text(value)
            with self.assertRaises(ValueError):
                load_fixtures(self.path)
        self.path.unlink()
        with self.assertRaises(ValueError):
            load_fixtures(self.path)

    def test_rejects_unsafe_urls(self):
        for url in [None, 123, 'https://example.com', 'file:///tmp/a',
                    'http://user:pass@localhost', 'http://localhost:99999',
                    'http://localhost:0', 'http://localhost/?x=1', 'http://localhost/#x']:
            with self.subTest(url=url), self.assertRaises(ValueError):
                validate_loopback_url(url)

    def test_credentials_cannot_inject_runner_arguments(self):
        for value in ['', 'key; command', 'key\nvalue', ['key']]:
            self.data['sdk-qa-a'] = value
            self.path.write_text(json.dumps(self.data))
            with self.assertRaises(ValueError):
                load_fixtures(self.path)

    def test_runners_validate_before_starting_commands(self):
        self.path.write_text('{}')
        client = Path(__file__).resolve().parents[1]
        for relative, args in [
            ('swift/qa/run-simulator-tests.py', ['--simulator', 'unused', '--result-bundle', 'unused']),
            ('kotlin/qa/run-device-tests.py', ['--serial', 'unused', '--output', 'unused'])
        ]:
            with self.subTest(runner=relative), patch('subprocess.run') as run, \
                    patch.object(sys, 'argv', ['runner', '--fixtures', str(self.path), *args]), \
                    contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as error:
                runpy.run_path(str(client / relative), run_name='__main__')
            self.assertEqual(error.exception.code, 2)
            run.assert_not_called()

    def test_runner_mapping(self):
        client = Path(__file__).resolve().parents[1]
        with patch('subprocess.run') as run, patch.object(sys, 'argv', [
            'runner', '--fixtures', str(self.path), '--simulator', 'test', '--result-bundle', 'unused'
        ]):
            runpy.run_path(str(client / 'swift/qa/run-simulator-tests.py'), run_name='__main__')
        self.assertEqual(run.call_args.kwargs['env']['TEST_RUNNER_NONA_FRONTEND_A'], 'test-a')
        self.assertEqual(run.call_args.kwargs['env']['TEST_RUNNER_NONA_FRONTEND_B'], 'test-b')
        with patch('subprocess.run', return_value=subprocess.CompletedProcess([], 0, 'OK (10 tests)')) as run, \
                patch.dict('os.environ', {'ANDROID_HOME': self.directory.name}), \
                patch.object(sys, 'argv', ['runner', '--fixtures', str(self.path), '--serial', 'test',
                                          '--output', str(Path(self.directory.name) / 'result')]), \
                contextlib.redirect_stdout(io.StringIO()):
            runpy.run_path(str(client / 'kotlin/qa/run-device-tests.py'), run_name='__main__')
        command = run.call_args.args[0]
        self.assertIn('http://127.0.0.1:18786', command)
        self.assertIn('http://127.0.0.1:18687', command)
        mappings = [call.args[0][-2:] for call in run.call_args_list if '--no-rebind' in call.args[0]]
        self.assertEqual(mappings, [['tcp:18687', 'tcp:18687'], ['tcp:18688', 'tcp:18688'], ['tcp:18786', 'tcp:18786']])
        self.assertIn('test-a', command)
        self.assertIn('test-b', command)

    def test_redirect_uses_each_emulators_host_alias(self):
        module = runpy.run_path(str(Path(__file__).with_name('fault-server.py')))
        target = module['redirect_target']
        for host in ['127.0.0.1', 'localhost', '10.0.2.2']:
            self.assertEqual(target(host + ':18687'), f'http://{host}:18688/capture')
        self.assertEqual(target('external.example:18687'), 'http://127.0.0.1:18688/capture')


class AdbTransportTests(unittest.TestCase):
    def configure(self, response, ports):
        module = runpy.run_path(str(Path(__file__).resolve().parents[1] / 'kotlin/qa/adb_transport.py'))
        with patch('subprocess.run', return_value=subprocess.CompletedProcess([], 0, response)) as run:
            module['configure_reverse'](['adb', '-s', 'qa-device'], ports)
        return run

    def test_reuses_existing_mapping(self):
        run = self.configure('device tcp:18686 tcp:18686\n', [18686, 18686])
        self.assertEqual(run.call_count, 1)

    def test_does_not_overwrite_other_mapping(self):
        with self.assertRaisesRegex(RuntimeError, 'Conflicting'):
            self.configure('device tcp:18686 tcp:9999\n', [18686])

    def test_reverse_failure_stops_before_installation(self):
        client = Path(__file__).resolve().parents[1]
        with tempfile.TemporaryDirectory() as directory:
            fixture = Path(directory) / 'fixtures.json'
            fixture.write_text(json.dumps({'baseUrl': 'http://127.0.0.1:18686', 'sdk-qa-a': 'a',
                'sdk-qa-b': 'b', 'backendKey': 'backend', 'adminToken': 'admin'}))
            with patch('subprocess.run', side_effect=subprocess.CalledProcessError(1, 'adb')) as run, \
                    patch.dict('os.environ', {'ANDROID_HOME': directory}), \
                    patch.object(sys, 'argv', ['runner', '--fixtures', str(fixture), '--serial', 'test', '--output', 'unused']), \
                    self.assertRaises(subprocess.CalledProcessError):
                runpy.run_path(str(client / 'kotlin/qa/run-device-tests.py'), run_name='__main__')
            self.assertEqual(run.call_count, 1)
            self.assertNotIn('install', run.call_args.args[0])


    def test_failed_suite_runs_once_and_saves_network_diagnostics(self):
        client = Path(__file__).resolve().parents[1]
        with tempfile.TemporaryDirectory() as directory:
            fixture = Path(directory) / 'fixtures.json'
            fixture.write_text(json.dumps({'baseUrl': 'http://127.0.0.1:18686', 'sdk-qa-a': 'a',
                'sdk-qa-b': 'b', 'backendKey': 'backend', 'adminToken': 'admin'}))
            output = Path(directory) / 'result.txt'
            def reply(command, **kwargs):
                text = 'FAILURES!!!' if 'instrument' in command else ''
                return subprocess.CompletedProcess(command, 0, text)
            with patch('subprocess.run', side_effect=reply) as run, \
                    patch.dict('os.environ', {'ANDROID_HOME': directory}), \
                    patch.object(sys, 'argv', ['runner', '--fixtures', str(fixture), '--serial', 'test', '--output', str(output)]), \
                    contextlib.redirect_stdout(io.StringIO()), self.assertRaises(SystemExit) as error:
                runpy.run_path(str(client / 'kotlin/qa/run-device-tests.py'), run_name='__main__')
            self.assertEqual(error.exception.code, 1)
            self.assertEqual(sum('instrument' in call.args[0] for call in run.call_args_list), 1)
            self.assertIn('ip route', output.with_suffix('.txt.network.txt').read_text())
