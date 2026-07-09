import io
import json
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path

import mutation_report


def report_with(*mutants):
    return {
        "files": {
            "src/Dekaf/Protocol/KafkaProtocolReader.cs": {
                "mutants": list(mutants),
            }
        }
    }


def mutant(status, line, *, name="logical", replacement="false"):
    return {
        "id": str(line),
        "mutatorName": name,
        "replacement": replacement,
        "status": status,
        "location": {
            "start": {"line": line, "column": 1},
            "end": {"line": line, "column": 2},
        },
    }


class MutationReportTests(unittest.TestCase):
    def test_summarize_counts_scored_mutants_and_prioritizes_backlog(self):
        report = report_with(
            mutant("Killed", 10),
            mutant("Timeout", 11),
            mutant("Survived", 30, name="equality", replacement="!= target"),
            mutant("NoCoverage", 20, name="boolean", replacement="true"),
            mutant("Ignored", 40),
            mutant("CompileError", 41),
            mutant("RuntimeError", 42),
        )

        summary = mutation_report.summarize(report)

        self.assertEqual(50.0, summary.score)
        self.assertEqual(2, summary.detected)
        self.assertEqual(2, summary.undetected)
        self.assertEqual(1, summary.statuses["Survived"])
        self.assertEqual(1, summary.statuses["NoCoverage"])
        self.assertEqual(
            ["Survived", "NoCoverage"],
            [entry.status for entry in summary.backlog],
        )
        self.assertEqual(30, summary.backlog[0].line)

    def test_render_reports_delta_and_flags_only_large_drop(self):
        previous = mutation_report.summarize(
            report_with(
                *[mutant("Killed", line) for line in range(1, 10)],
                mutant("Survived", 20),
            )
        )
        current = mutation_report.summarize(
            report_with(
                *[mutant("Killed", line) for line in range(1, 8)],
                *[mutant("Survived", line) for line in range(20, 23)],
            )
        )

        markdown, failed = mutation_report.render_markdown(current, previous, max_drop=5.0)

        self.assertTrue(failed)
        self.assertIn("70.00%", markdown)
        self.assertIn("-20.00 percentage points", markdown)
        self.assertIn("large-drop gate: failed", markdown.lower())

        _, small_drop_failed = mutation_report.render_markdown(previous, previous, max_drop=5.0)
        self.assertFalse(small_drop_failed)

    def test_cli_writes_summary_and_returns_failure_for_large_drop(self):
        current = report_with(mutant("Killed", 1), mutant("Survived", 2))
        previous = report_with(mutant("Killed", 1), mutant("Killed", 2))

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            current_path = root / "current.json"
            previous_path = root / "previous.json"
            summary_path = root / "summary.md"
            current_path.write_text(json.dumps(current), encoding="utf-8")
            previous_path.write_text(json.dumps(previous), encoding="utf-8")

            stdout = io.StringIO()
            with redirect_stdout(stdout):
                exit_code = mutation_report.main(
                    [
                        str(current_path),
                        "--previous",
                        str(previous_path),
                        "--max-drop",
                        "5",
                        "--summary",
                        str(summary_path),
                    ]
                )

            self.assertEqual(1, exit_code)
            self.assertIn(
                "large-drop gate: failed",
                summary_path.read_text(encoding="utf-8").lower(),
            )
            self.assertIn("::error::", stdout.getvalue())


if __name__ == "__main__":
    unittest.main()
