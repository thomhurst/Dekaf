import json
import os
import shutil
import stat
import subprocess
import sys
import tempfile
import textwrap
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("update-release-draft.sh")
REPO = "repos/o/r"
MARKER = "<!-- dekaf-release-draft -->"

# A stand-in for `gh api` that serves GET responses from a scenario file and records every
# request. Unlisted GETs return 404. `--jq` is evaluated with jq, as gh does.
FAKE_GH = textwrap.dedent("""\
    import json, os, subprocess, sys
    args = sys.argv[2:]
    include = method = jq = None
    fields, path = [], None
    i = 0
    while i < len(args):
        a = args[i]
        if a == "-i": include = True
        elif a == "-X": method = args[i + 1]; i += 1
        elif a == "--paginate": pass
        elif a == "--jq": jq = args[i + 1]; i += 1
        elif a in ("-f", "-F"): fields.append(args[i + 1]); i += 1
        else: path = a
        i += 1
    method = method or "GET"
    scenario = json.load(open(os.environ["FAKE_GH_SCENARIO"]))
    with open(os.environ["FAKE_GH_LOG"], "a") as log:
        log.write(json.dumps({"method": method, "path": path, "fields": fields}) + "\\n")
    if method == "GET":
        status, body = scenario.get(path, [404, {"message": "Not Found"}])
    else:
        status, body = scenario.get(method + " " + path, [200, {"html_url": "https://x/draft", "body": "notes"}])
    text = json.dumps(body)
    if include:
        # gh prints the status line with LF and the headers with CRLF, then the body.
        sys.stdout.write(f"HTTP/2.0 {status} X\\nContent-Type: application/json\\r\\n\\r\\n{text}")
        sys.exit(0 if status == 200 else 1)
    if status != 200:
        print(f"gh: failed (HTTP {status})", file=sys.stderr)
        sys.exit(1)
    if jq:
        text = subprocess.run(["jq", "-r", jq], input=text, capture_output=True, text=True, check=True).stdout
    sys.stdout.write(text if text.endswith("\\n") or not text else text + "\\n")
""")


class UpdateReleaseDraftTests(unittest.TestCase):
    def setUp(self) -> None:
        if shutil.which("jq") is None:
            self.skipTest("jq is not installed")
        self.directory = tempfile.TemporaryDirectory()
        self.root = Path(self.directory.name)
        bin_dir = self.root / "bin"
        bin_dir.mkdir()
        (self.root / "fake_gh.py").write_text(FAKE_GH)
        gh = bin_dir / "gh"
        gh.write_text(f'#!/bin/sh\nexec "{sys.executable}" "{self.root / "fake_gh.py"}" "$@"\n')
        gh.chmod(gh.stat().st_mode | stat.S_IEXEC)
        self.path = f"{bin_dir}{os.pathsep}{os.environ['PATH']}"

    def tearDown(self) -> None:
        self.directory.cleanup()

    def run_script(self, scenario: dict, version: str = "1.29.1") -> tuple[subprocess.CompletedProcess[str], list[dict]]:
        base = {
            f"{REPO}/releases/latest": [200, {"tag_name": "v1.29.0"}],
            f"POST {REPO}/releases/generate-notes": [200, {"body": "## What's Changed"}],
            f"{REPO}/releases?per_page=100": [200, []],
            f"{REPO}/git/ref/heads/main": [200, {"object": {"sha": "abc"}}],
        }
        base.update(scenario)
        (self.root / "scenario.json").write_text(json.dumps(base))
        log = self.root / "log"
        log.write_text("")
        result = subprocess.run(
            ["bash", str(SCRIPT)], capture_output=True, text=True,
            env={
                "PATH": self.path, "GITHUB_REPOSITORY": "o/r", "GITHUB_SHA": "abc", "VERSION": version,
                "DRAFT_MARKER": MARKER, "FAKE_GH_SCENARIO": str(self.root / "scenario.json"),
                "FAKE_GH_LOG": str(log),
            },
        )
        calls = [json.loads(line) for line in log.read_text().splitlines()]
        return result, calls

    @staticmethod
    def writes(calls: list[dict]) -> list[dict]:
        return [c for c in calls if c["method"] in ("POST", "PATCH") and not c["path"].endswith("generate-notes")]

    def test_creates_a_draft_when_none_is_owned(self) -> None:
        result, calls = self.run_script({
            f"{REPO}/releases?per_page=100": [200, [{"id": 5, "draft": True, "body": "someone else's"}]],
        })
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        [write] = self.writes(calls)
        self.assertEqual(("POST", f"{REPO}/releases"), (write["method"], write["path"]))
        self.assertIn("draft=true", write["fields"])
        self.assertIn("tag_name=v1.29.1", write["fields"])
        self.assertIn("target_commitish=abc", write["fields"])
        self.assertIn(f"body=## What's Changed\n\n{MARKER}", write["fields"])
        notes = next(c for c in calls if c["path"].endswith("generate-notes"))
        self.assertIn("previous_tag_name=v1.29.0", notes["fields"])

    def test_updates_the_newest_owned_draft_without_sending_draft(self) -> None:
        result, calls = self.run_script({
            f"{REPO}/releases?per_page=100": [200, [
                {"id": 7, "draft": True, "body": f"old {MARKER}"},
                {"id": 9, "draft": True, "body": f"newer {MARKER}"},
            ]],
            f"{REPO}/releases/9": [200, {"id": 9, "draft": True}],
        })
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        [write] = self.writes(calls)
        self.assertEqual(("PATCH", f"{REPO}/releases/9"), (write["method"], write["path"]))
        self.assertFalse(any(f.startswith("draft=") for f in write["fields"]))
        self.assertIn("updating 9 and leaving 7 alone", result.stdout)

    def draft_tagged(self, tag: str, extra: dict | None = None) -> tuple[subprocess.CompletedProcess[str], list[dict]]:
        scenario = {
            f"{REPO}/releases?per_page=100": [200, [{"id": 9, "draft": True, "body": MARKER}]],
            f"{REPO}/releases/9": [200, {"id": 9, "draft": True, "tag_name": tag, "name": tag}],
        }
        scenario.update(extra or {})
        return self.run_script(scenario)

    def test_keeps_a_higher_version_the_owner_set(self) -> None:
        for tag in ("v1.30.0", "1.30.0", "v2.0.0-beta.1", "v1.29.2"):
            with self.subTest(tag=tag):
                result, calls = self.draft_tagged(tag)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                [write] = self.writes(calls)
                self.assertEqual(("PATCH", f"{REPO}/releases/9"), (write["method"], write["path"]))
                self.assertEqual(["target_commitish=abc"], [f for f in write["fields"] if not f.startswith("body=")])
                notes = next(c for c in calls if c["path"].endswith("generate-notes"))
                self.assertIn(f"tag_name={tag}", notes["fields"])
                self.assertIn(f"kept the draft's tag {tag}", result.stdout)

    def test_uses_the_suggestion_over_a_lower_equal_invalid_or_taken_tag(self) -> None:
        for tag, extra in (
            ("v1.29.0", None), ("v1.29.1", None), ("1.29.1", None), ("v1.29.1-rc.1", None),
            ("v1.30.01", None), ("next", None), ("", None),
            ("v1.30.0", {f"{REPO}/git/ref/tags/v1.30.0": [200, {}]}),
            ("v1.30.0", {f"{REPO}/releases/tags/1.30.0": [200, {}]}),
        ):
            with self.subTest(tag=tag, extra=extra):
                result, calls = self.draft_tagged(tag, extra)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                [write] = self.writes(calls)
                self.assertIn("tag_name=v1.29.1", write["fields"])
                self.assertIn("name=1.29.1", write["fields"])
                notes = next(c for c in calls if c["path"].endswith("generate-notes"))
                self.assertIn("tag_name=v1.29.1", notes["fields"])
                self.assertIn("used the suggested tag v1.29.1", result.stdout)

    def test_leaves_a_draft_published_since_it_was_selected(self) -> None:
        result, calls = self.run_script({
            f"{REPO}/releases?per_page=100": [200, [{"id": 9, "draft": True, "body": MARKER}]],
            f"{REPO}/releases/9": [200, {"id": 9, "draft": False}],
        })
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual([], self.writes(calls))
        self.assertIn("was published since it was selected", result.stdout)

    def test_leaves_the_draft_alone_when_the_version_is_already_tagged_or_released(self) -> None:
        for path in ("git/ref/tags/v1.29.1", "git/ref/tags/1.29.1", "releases/tags/v1.29.1", "releases/tags/1.29.1"):
            with self.subTest(path=path):
                result, calls = self.run_script({
                    f"{REPO}/{path}": [200, {}],
                    f"{REPO}/releases?per_page=100": [200, [{"id": 9, "draft": True, "body": MARKER}]],
                    f"{REPO}/releases/9": [200, {"id": 9, "draft": True}],
                })
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertEqual([], self.writes(calls))
                self.assertIn("already tagged or released", result.stdout)

    def test_omits_the_previous_tag_when_nothing_is_released(self) -> None:
        result, calls = self.run_script({f"{REPO}/releases/latest": [404, {"message": "Not Found"}]})
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        notes = next(c for c in calls if c["path"].endswith("generate-notes"))
        self.assertFalse(any(f.startswith("previous_tag_name=") for f in notes["fields"]))

    def test_a_failed_lookup_fails_without_writing(self) -> None:
        for path in ("git/ref/tags/v1.29.1", "releases/latest", "releases?per_page=100"):
            with self.subTest(path=path):
                result, calls = self.run_script({f"{REPO}/{path}": [503, {"message": "Unavailable"}]})
                self.assertNotEqual(0, result.returncode)
                self.assertEqual([], self.writes(calls))

    def test_a_failed_lookup_of_the_owner_tag_fails_without_writing(self) -> None:
        result, calls = self.draft_tagged("v1.30.0", {f"{REPO}/releases/tags/v1.30.0": [502, {}]})
        self.assertNotEqual(0, result.returncode)
        self.assertEqual([], self.writes(calls))

    def test_a_run_for_an_older_commit_leaves_the_draft(self) -> None:
        for drafts in ([], [{"id": 9, "draft": True, "body": MARKER}]):
            with self.subTest(drafts=drafts):
                result, calls = self.run_script({
                    f"{REPO}/releases?per_page=100": [200, drafts],
                    f"{REPO}/releases/9": [200, {"id": 9, "draft": True, "tag_name": "v1.29.1"}],
                    f"{REPO}/git/ref/heads/main": [200, {"object": {"sha": "newer"}}],
                })
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertEqual([], self.writes(calls))
                self.assertIn("is no longer the tip of main", result.stdout)

    def test_a_failed_branch_lookup_fails_without_writing(self) -> None:
        for status in (404, 500):
            with self.subTest(status=status):
                result, calls = self.run_script({f"{REPO}/git/ref/heads/main": [status, {}]})
                self.assertNotEqual(0, result.returncode)
                self.assertEqual([], self.writes(calls))

    def test_failed_notes_generation_writes_nothing(self) -> None:
        for drafts in ([], [{"id": 9, "draft": True, "body": MARKER}]):
            with self.subTest(drafts=drafts):
                result, calls = self.run_script({
                    f"{REPO}/releases?per_page=100": [200, drafts],
                    f"{REPO}/releases/9": [200, {"id": 9, "draft": True, "tag_name": "v1.29.1"}],
                    f"POST {REPO}/releases/generate-notes": [500, {"message": "boom"}],
                })
                self.assertNotEqual(0, result.returncode)
                self.assertEqual([], self.writes(calls))

    def test_a_missing_version_fails(self) -> None:
        result, calls = self.run_script({}, version="")
        self.assertEqual(1, result.returncode)
        self.assertEqual([], calls)


if __name__ == "__main__":
    unittest.main()
