import subprocess
import unittest
from pathlib import Path


LIBRARY = Path(__file__).with_name("semver.sh")


def bash(snippet: str, *args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["bash", "-c", f'set -euo pipefail; source "{LIBRARY}"; {snippet}', "bash", *args],
        capture_output=True, text=True,
    )


class SemverTests(unittest.TestCase):
    def compare(self, a: str, b: str) -> int:
        result = bash('semver_compare "$1" "$2"', a, b)
        self.assertEqual(0, result.returncode, result.stderr)
        return int(result.stdout)

    def test_precedence(self) -> None:
        ascending = [
            "0.0.0", "0.0.1", "0.1.0", "1.0.0-0", "1.0.0-1", "1.0.0-alpha", "1.0.0-alpha.1",
            "1.0.0-alpha.beta", "1.0.0-beta", "1.0.0-beta.2", "1.0.0-beta.11", "1.0.0-rc.1", "1.0.0",
            "1.9.0", "1.10.0", "1.10.1", "1.11.0", "2.0.0-rc.9", "2.0.0-rc.10", "2.0.0", "10.0.0",
            "99999999999999999999.0.0",
        ]
        pairs = list(zip(ascending, ascending[1:])) + [
            ("1.0.0-alpha", "1.0.0-beta"), ("1.0.0-1", "1.0.0-alpha"), ("2.0.0-rc.1", "2.0.0"),
            ("1.10.1", "1.11.0"),
        ]
        for lower, higher in pairs:
            with self.subTest(lower=lower, higher=higher):
                self.assertEqual(-1, self.compare(lower, higher))
                self.assertEqual(1, self.compare(higher, lower))

    def test_equal_versions_and_the_v_prefix(self) -> None:
        for a, b in (("1.2.3", "1.2.3"), ("v1.2.3", "1.2.3"), ("v2.0.0-rc.1", "2.0.0-rc.1")):
            with self.subTest(a=a, b=b):
                self.assertEqual(0, self.compare(a, b))
                self.assertNotEqual(0, bash('semver_gt "$1" "$2"', a, b).returncode)

    def test_semver_gt(self) -> None:
        self.assertEqual(0, bash('semver_gt "$1" "$2"', "v1.11.0", "1.10.1").returncode)
        self.assertNotEqual(0, bash('semver_gt "$1" "$2"', "1.10.1", "v1.11.0").returncode)

    def test_is_version(self) -> None:
        for tag in ("v1.2.3", "1.2.3", "v0.0.0", "v1.2.3-beta.1", "v1.2.3-beta01", "v1.2.3-0"):
            with self.subTest(tag=tag):
                self.assertEqual(0, bash('is_version "$1"', tag).returncode)
        for tag in ("v1.02.3", "v01.2.3", "v1.2.3-beta..1", "v1.2.3-beta.01", "v1.2.3-01", "v1.2.3-",
                    "v1.2", "release-1", "v1.2.3+build", ""):
            with self.subTest(tag=tag):
                self.assertNotEqual(0, bash('is_version "$1"', tag).returncode)


if __name__ == "__main__":
    unittest.main()
