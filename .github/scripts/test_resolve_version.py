import os
import subprocess
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("resolve-version.sh")


def git(repo: Path, *args: str) -> str:
    return subprocess.run(
        ["git", "-c", "user.name=test", "-c", "user.email=test@example.invalid",
         "-c", "commit.gpgSign=false", *args],
        cwd=repo, check=True, capture_output=True, text=True,
    ).stdout.strip()


class ResolveVersionTests(unittest.TestCase):
    def setUp(self) -> None:
        self.directory = tempfile.TemporaryDirectory()
        self.root = Path(self.directory.name)
        self.repo = self.root / "repo"
        self.repo.mkdir()
        git(self.repo, "init", "-q", "-b", "main")
        git(self.repo, "commit", "-q", "--allow-empty", "-m", "on main")
        self.main_sha = git(self.repo, "rev-parse", "HEAD")
        git(self.repo, "checkout", "-q", "-b", "side")
        git(self.repo, "commit", "-q", "--allow-empty", "-m", "off main")
        self.side_sha = git(self.repo, "rev-parse", "HEAD")
        # actions/checkout with fetch-depth 0 provides the default branch as origin/<branch>.
        git(self.repo, "update-ref", "refs/remotes/origin/main", self.main_sha)

    def tearDown(self) -> None:
        self.directory.cleanup()

    def run_script(self, **env: str) -> tuple[subprocess.CompletedProcess[str], str, str]:
        github_env = self.root / "env"
        github_output = self.root / "output"
        github_env.write_text("")
        github_output.write_text("")
        result = subprocess.run(
            ["bash", str(SCRIPT)],
            cwd=self.repo, capture_output=True, text=True,
            env={
                "PATH": os.environ["PATH"],
                "GITHUB_ENV": str(github_env),
                "GITHUB_OUTPUT": str(github_output),
                "GITHUB_SHA": self.main_sha,
                **env,
            },
        )
        return result, github_env.read_text(), github_output.read_text()

    def test_release_takes_the_version_from_a_semver_tag(self) -> None:
        for tag, version in (
            ("v1.29.1", "1.29.1"),
            ("1.30.0", "1.30.0"),
            ("v2.0.0-beta.1", "2.0.0-beta.1"),
            ("v2.0.0-rc.10", "2.0.0-rc.10"),
        ):
            with self.subTest(tag=tag):
                result, github_env, github_output = self.run_script(
                    GITHUB_EVENT_NAME="release", RELEASE_TAG=tag, GITVERSION_SEMVER="9.9.9")
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertEqual(f"DEKAF_VERSION={version}\n", github_env)
                self.assertEqual(f"semVer={version}\n", github_output)

    def test_release_rejects_a_tag_that_is_not_a_version(self) -> None:
        for tag in ("", "latest", "v1.2", "v1.2.3.4", "vv1.2.3", "v01.2.3", "v1.2.3-", "v1.2.3+build",
                    "v1.2.3-beta..1", "release-1.2.3", "v1.2.3 ", "v1.2.3\nv1.2.4"):
            with self.subTest(tag=tag):
                result, github_env, _ = self.run_script(GITHUB_EVENT_NAME="release", RELEASE_TAG=tag)
                self.assertEqual(1, result.returncode)
                self.assertIn("is not a version", result.stdout)
                self.assertEqual("", github_env)

    def test_release_rejects_a_commit_that_is_not_on_the_default_branch(self) -> None:
        result, github_env, _ = self.run_script(
            GITHUB_EVENT_NAME="release", RELEASE_TAG="v1.29.1", GITHUB_SHA=self.side_sha)
        self.assertEqual(1, result.returncode)
        self.assertIn("which is not on main", result.stdout)
        self.assertEqual("", github_env)

    def test_other_runs_use_the_gitversion_version(self) -> None:
        result, github_env, github_output = self.run_script(
            GITHUB_EVENT_NAME="pull_request", RELEASE_TAG="", GITVERSION_SEMVER="1.29.1-pr.7.1")
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual("DEKAF_VERSION=1.29.1-pr.7.1\n", github_env)
        self.assertEqual("semVer=1.29.1-pr.7.1\n", github_output)

    def test_other_runs_export_nothing_when_gitversion_failed(self) -> None:
        result, github_env, github_output = self.run_script(GITHUB_EVENT_NAME="workflow_dispatch")
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual("", github_env)
        self.assertEqual("", github_output)


if __name__ == "__main__":
    unittest.main()
