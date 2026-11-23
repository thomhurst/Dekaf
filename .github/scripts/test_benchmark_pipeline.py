import json
import re
import tempfile
import unittest
from pathlib import Path

from benchmark_pipeline import pipeline_mode, shard_filters, validate_gate, validate_reports


LISTING = "\n".join([
    "Dekaf.Benchmarks.Benchmarks.Unit.Alpha.One",
    "Dekaf.Benchmarks.Benchmarks.Unit.Alpha.Two",
    "Dekaf.Benchmarks.Benchmarks.Unit.AlphaBeta.One",
    "Dekaf.Benchmarks.Benchmarks.Unit.Cache<Int32>.Hit",
    "Dekaf.Benchmarks.Benchmarks.Unit.Cache<Object>.Hit",
    "Dekaf.Benchmarks.Benchmarks.Unit.Zeta.Run",
    "",
    "// unrelated log line",
])


class BenchmarkPipelineTests(unittest.TestCase):
    def test_shards_cover_every_class_exactly_once(self):
        for count in (1, 2, 3, 5, 8):
            with self.subTest(count=count):
                shards = [shard_filters(LISTING, index, count) for index in range(1, count + 1)]
                flattened = [item for shard in shards for item in shard]
                self.assertEqual(sorted(flattened), sorted(set(flattened)))
                self.assertEqual({
                    "Dekaf.Benchmarks.Benchmarks.Unit.Alpha.*",
                    "Dekaf.Benchmarks.Benchmarks.Unit.AlphaBeta.*",
                    "Dekaf.Benchmarks.Benchmarks.Unit.Cache<Int32>.*",
                    "Dekaf.Benchmarks.Benchmarks.Unit.Cache<Object>.*",
                    "Dekaf.Benchmarks.Benchmarks.Unit.Zeta.*",
                }, set(flattened))
                self.assertLessEqual(max(map(len, shards)) - min(map(len, shards)), 1)

    def test_shard_filters_do_not_match_classes_sharing_a_prefix(self):
        self.assertIn("Dekaf.Benchmarks.Benchmarks.Unit.Alpha.*", shard_filters(LISTING, 1, 1))
        self.assertNotIn("Dekaf.Benchmarks.Benchmarks.Unit.Alpha*", shard_filters(LISTING, 1, 1))

    def test_invalid_shards_or_empty_listing_fail(self):
        for shard, count in ((0, 3), (4, 3), (1, 0)):
            with self.subTest(shard=shard, count=count), self.assertRaises(ValueError):
                shard_filters(LISTING, shard, count)
        with self.assertRaises(ValueError):
            shard_filters("no benchmarks here\n", 1, 1)

    def test_extra_shards_receive_no_filters(self):
        self.assertEqual([], shard_filters(LISTING, 8, 8))

    @staticmethod
    def workflow_job(job):
        workflow = (Path(__file__).resolve().parents[1] / "workflows/benchmarks.yml").read_text(encoding="utf-8")
        start = workflow.index(f"\n  {job}:\n")
        following = re.search(r"\n  [a-z-]+:\n", workflow[start + 1:])
        return workflow[start:start + 1 + following.start()] if following else workflow[start:]

    def test_workflow_shards_cover_the_declared_count(self):
        for job, count in (("unit-benchmarks", 16), ("client-benchmarks-native-memory", 8)):
            with self.subTest(job=job):
                body = self.workflow_job(job)
                self.assertIn(f"SHARD_COUNT: {count}", body)
                self.assertIn(f"shard: [{', '.join(str(index) for index in range(1, count + 1))}]", body)
                self.assertIn("benchmark_pipeline.py shard", body)
                self.assertIn("--buildTimeout 600", body)

    def test_client_run_allows_slow_generated_builds(self):
        self.assertIn("--buildTimeout 600", self.workflow_job("client-benchmarks"))
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
