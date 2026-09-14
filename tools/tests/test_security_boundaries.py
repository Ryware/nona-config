"""Regression checks for deployment exposure and the developer hook's temp files."""
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


class SecurityBoundaries(unittest.TestCase):
    def test_replication_publishes_only_nona_api(self):
        for mode in ("dev", "prod"):
            text = (ROOT / f"deploy/compose/primary-replica-{mode}.yml").read_text()
            # Inspect every host port mapping, independently of its chosen host port.
            mappings = re.findall(r'^\s+- "\$\{[^}]+\}:(\d+)"\s*$', text, re.M)
            self.assertEqual(mappings, ["8080", "8080"])
            self.assertIn("http://nona-primary:5001", text)
            self.assertIn("http://nona-primary:9080", text)

    def test_hook_does_not_follow_precreated_symlinks(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            tools = root / "bin"
            tools.mkdir()
            hook = root / "pre-push"
            shutil.copyfile(ROOT / ".githooks/pre-push", hook)
            hook.chmod(0o700)
            git = tools / "git"
            git.write_text('#!/bin/sh\ncase "$*" in *--show-toplevel*) printf "%s\\n" "$TEST_ROOT";; esac\n')
            git.chmod(0o700)
            target = root / "victim.txt"
            target.write_text("must survive")
            temp = root / "shared temp"
            temp.mkdir()
            env = dict(os.environ, PATH=f"{tools}:{os.environ['PATH']}", TEST_ROOT=str(root),
                       TMPDIR=str(temp), TEST_HOOK=str(hook), TEST_TARGET=str(target))
            script = '''
                directory="$TMPDIR/nona-pre-push.$$"
                mkdir "$directory"
                ln -s "$TEST_TARGET" "$directory/refs"
                ln -s "$TEST_TARGET" "$directory/cs-files"
                ln -s "$TEST_TARGET" "$directory/dotnet-files"
                exec "$TEST_HOOK" </dev/null
            '''
            result = subprocess.run(["sh", "-c", script], env=env, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIn("No pushed .NET files", result.stdout)
            self.assertEqual(target.read_text(), "must survive")
            # Only the adversarial directory remains; the hook removes its own allocation.
            self.assertEqual(len(list(temp.iterdir())), 1)

    def test_hook_fails_when_temp_allocation_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            tools = Path(directory)
            for name, body in [("git", 'printf "%s\\n" "$TMPDIR"'), ("mktemp", "exit 1")]:
                executable = tools / name
                executable.write_text(f"#!/bin/sh\n{body}\n")
                executable.chmod(0o700)
            result = subprocess.run(["sh", str(ROOT / ".githooks/pre-push")],
                                    env=dict(os.environ, TMPDIR=directory, PATH=f"{tools}:{os.environ['PATH']}"),
                                    capture_output=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(sorted(p.name for p in tools.iterdir()), ["git", "mktemp"])


if __name__ == "__main__":
    unittest.main()
