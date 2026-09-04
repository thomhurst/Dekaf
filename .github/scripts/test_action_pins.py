import re
import unittest
from pathlib import Path


WORKFLOW_DIRECTORY = Path(__file__).parents[1] / "workflows"
TARGET_ACTIONS = {
    "benchmark-action/github-action-benchmark",
    "gittools/actions/gitversion/execute",
    "gittools/actions/gitversion/setup",
}
PIN_PATTERN = re.compile(
    r"^\s*uses:\s*(?P<action>[^@\s]+)@(?P<sha>[0-9a-f]{40})\s+#\s+"
    r"(?P<tag>v\d+\.\d+\.\d+)\s*$"
)


class ActionPinTests(unittest.TestCase):
    def test_remote_actions_are_pinned_to_full_commit_shas(self) -> None:
        for workflow_path in WORKFLOW_DIRECTORY.iterdir():
            if workflow_path.suffix not in {".yml", ".yaml"}:
                continue

            for line_number, line in enumerate(
                workflow_path.read_text(encoding="utf-8").splitlines(), start=1
            ):
                match = re.match(r"^\s*(?:-\s+)?uses:\s*([^\s#]+)", line)
                if match is None:
                    continue

                action = match.group(1).strip("\"'")
                if action.startswith(("./", "docker://")):
                    continue

                with self.subTest(workflow=workflow_path.name, line=line_number):
                    self.assertRegex(
                        action,
                        r"^[^@]+@[0-9a-fA-F]{40}$",
                        "Remote actions must be pinned to a full-length commit SHA",
                    )

    def test_actions_without_moving_major_tags_use_exact_release_comments(self) -> None:
        found_actions: set[str] = set()

        for workflow_path in WORKFLOW_DIRECTORY.glob("*.yml"):
            for line_number, line in enumerate(
                workflow_path.read_text(encoding="utf-8").splitlines(), start=1
            ):
                if not any(f"uses: {action}@" in line for action in TARGET_ACTIONS):
                    continue

                match = PIN_PATTERN.match(line)
                self.assertIsNotNone(
                    match,
                    f"{workflow_path}:{line_number} must use a SHA pin with an exact "
                    "release tag comment",
                )
                found_actions.add(match.group("action"))

        self.assertEqual(TARGET_ACTIONS, found_actions)


if __name__ == "__main__":
    unittest.main()
