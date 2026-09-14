"""Behavioral tests for preparing an immutable shared release tag."""

import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / ".github/scripts/prepare-release-tag.sh"


def find_bash():
    candidate = shutil.which("bash")
    if candidate:
        return candidate
    windows_git_bash = Path(r"C:\Program Files\Git\bin\bash.exe")
    if windows_git_bash.exists():
        return str(windows_git_bash)
    raise unittest.SkipTest("bash is required to test the release tag script")


class ReleaseTagPreparation(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.root = Path(self.directory.name)
        self.origin = self.root / "origin.git"
        self.checkout = self.root / "checkout"
        subprocess.run(["git", "init", "--bare", str(self.origin)], check=True,
                       capture_output=True)
        subprocess.run(["git", "clone", str(self.origin), str(self.checkout)], check=True,
                       capture_output=True)
        self.git("config", "user.name", "Release Test")
        self.git("config", "user.email", "release-test@example.com")
        (self.checkout / "README.md").write_text("first\n")
        self.git("add", "README.md")
        self.git("commit", "-m", "first")
        self.git("branch", "-M", "master")
        self.git("push", "-u", "origin", "master")
        self.first_sha = self.git("rev-parse", "HEAD").stdout.strip()

    def tearDown(self):
        self.directory.cleanup()

    def git(self, *args):
        return subprocess.run(["git", *args], cwd=self.checkout, check=True,
                              capture_output=True, text=True)

    def prepare(self, version, source_ref="refs/heads/master", sha=None):
        output = self.root / "github-output.txt"
        return subprocess.run(
            [find_bash(), str(SCRIPT).replace("\\", "/"), version, source_ref, sha or self.first_sha],
            cwd=self.checkout,
            env=dict(os.environ, GITHUB_OUTPUT=str(output)),
            capture_output=True,
            text=True,
        )

    def remote_tag_sha(self, version):
        result = subprocess.run(
            ["git", "--git-dir", str(self.origin), "rev-parse", f"refs/tags/{version}^{{commit}}"],
            check=True,
            capture_output=True,
            text=True,
        )
        return result.stdout.strip()

    def test_creates_stable_version_tag_at_requested_master_commit(self):
        result = self.prepare("4.0.0")

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self.remote_tag_sha("4.0.0"), self.first_sha)

    def test_rejects_non_stable_versions_without_creating_a_tag(self):
        for version in ("v4.0.0", "cli-v4.0.0", "4.0", "4.0.0-beta.1", "04.0.0", "4.0.0/x"):
            with self.subTest(version=version):
                result = self.prepare(version)
                self.assertNotEqual(result.returncode, 0)
        tags = subprocess.run(["git", "--git-dir", str(self.origin), "tag"], check=True,
                              capture_output=True, text=True)
        self.assertEqual(tags.stdout, "")

    def test_rejects_a_dispatch_not_started_from_master(self):
        result = self.prepare("4.0.0", source_ref="refs/heads/development")

        self.assertNotEqual(result.returncode, 0)
        self.assertIn("master", result.stderr)

    def test_accepts_an_existing_tag_only_at_the_requested_commit(self):
        self.assertEqual(self.prepare("4.0.0").returncode, 0)
        same_commit = self.prepare("4.0.0")

        (self.checkout / "README.md").write_text("second\n")
        self.git("add", "README.md")
        self.git("commit", "-m", "second")
        self.git("push", "origin", "master")
        second_sha = self.git("rev-parse", "HEAD").stdout.strip()
        different_commit = self.prepare("4.0.0", sha=second_sha)

        self.assertEqual(same_commit.returncode, 0, same_commit.stderr)
        self.assertNotEqual(different_commit.returncode, 0)
        self.assertIn("different commit", different_commit.stderr)
        self.assertEqual(self.remote_tag_sha("4.0.0"), self.first_sha)


if __name__ == "__main__":
    unittest.main()
