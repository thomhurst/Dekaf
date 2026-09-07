import json
import tempfile
import unittest
from pathlib import Path

from benchmark_pipeline import pipeline_mode, validate_gate, validate_reports


class BenchmarkPipelineTests(unittest.TestCase):
    def test_only_explicit_manual_filters_select_filtered_pipeline(self):
        for event, value, expected in (
            ("schedule", "", "full"), ("schedule", "*Serializer*", "full"),
            ("workflow_dispatch", "*", "full"), ("workflow_dispatch", "", "full"),
            ("workflow_dispatch", "*Serializer*", "filtered"),
            ("workflow_dispatch", "$(touch unwanted)", "filtered"),
            ("workflow_dispatch", " ", "filtered"),
        ):
            with self.subTest(event=event, value=value):
                self.assertEqual(expected, pipeline_mode(event, value))

    def test_filtered_gate_requires_only_filtered_work_even_on_main(self):
        results = dict(PLAN="success", FILTERED="success", UNIT="skipped", CLIENT="skipped",
                       NATIVE="skipped", SUMMARY="skipped", HISTORY="skipped", DOCS="skipped")
        validate_gate("filtered", True, results)
        for name in results:
            for unexpected in ("failure", "cancelled", "success" if results[name] == "skipped" else "skipped"):
                with self.subTest(name=name, state=unexpected), self.assertRaises(ValueError):
                    validate_gate("filtered", True, {**results, name: unexpected})

    def test_full_gate_requires_publication_only_on_main(self):
        results = dict(PLAN="success", FILTERED="skipped", UNIT="success", CLIENT="success",
                       NATIVE="success", SUMMARY="success", HISTORY="skipped", DOCS="skipped")
        validate_gate("full", False, results)
        with self.assertRaises(ValueError):
            validate_gate("full", True, results)
        validate_gate("full", True, {**results, "HISTORY": "success", "DOCS": "success"})
        with self.assertRaises(ValueError):
            validate_gate("", False, results)

    def test_reports_count_all_successful_cases(self):
        with tempfile.TemporaryDirectory() as directory:
            for name in ("One", "Two"):
                self.write_report(directory, name, [{"Mean": 0.0, "N": 1}, {"Mean": 42.0, "N": 15}])
            self.assertEqual(4, validate_reports(directory))

    def test_empty_or_malformed_reports_fail(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(ValueError):
                validate_reports(directory)
            path = Path(directory) / "Bad-report-full.json"
            for text in ("not json", "{}", '{"Benchmarks": []}'):
                path.write_text(text, encoding="utf-8")
                with self.subTest(text=text), self.assertRaises(ValueError):
                    validate_reports(directory)

    def test_one_failed_case_cannot_hide_behind_successful_cases(self):
        with tempfile.TemporaryDirectory() as directory:
            self.write_report(directory, "Good", [{"Mean": 42, "N": 3}])
            for statistics in (None, {}, {"Mean": None, "N": 3}, {"Mean": float("nan"), "N": 3},
                               {"Mean": float("inf"), "N": 3}, {"Mean": -1, "N": 3},
                               {"Mean": 42, "N": 0}, {"Mean": 42, "N": True}):
                self.write_report(directory, "Bad", [statistics])
                with self.subTest(statistics=statistics), self.assertRaises(ValueError):
                    validate_reports(directory)

    @staticmethod
    def write_report(directory, name, statistics):
        payload = {"Benchmarks": [{"FullName": f"{name}.{index}", "Statistics": item}
                                   for index, item in enumerate(statistics)]}
        (Path(directory) / f"{name}-report-full.json").write_text(json.dumps(payload), encoding="utf-8")


if __name__ == "__main__":
    unittest.main()
