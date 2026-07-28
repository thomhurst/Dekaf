import json
import tempfile
import unittest
from pathlib import Path

from benchmark_docs import (
    annotate_ratio_confidence,
    format_rolling_table,
    generate_document,
    load_history,
    rolling_comparisons,
)


def benchmark(name, value):
    return {"name": name, "value": value, "unit": "ns", "range": "± 1"}


def history_entry(dekaf, confluent):
    prefix = "Dekaf.Benchmarks.Benchmarks.Client.ConsumerPollBenchmarks"
    parameters = "(MessageSize: 100)"
    return {
        "benches": [
            benchmark(f"{prefix}.Dekaf_PollSingle{parameters}", dekaf),
            benchmark(f"{prefix}.Confluent_PollSingle{parameters}", confluent),
        ]
    }


class BenchmarkDocsTests(unittest.TestCase):
    def test_load_history_parses_javascript_assignment(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "data.js"
            payload = {"entries": {"Dekaf Benchmarks": [history_entry(200, 100)]}}
            path.write_text(
                f"window.BENCHMARK_DATA = {json.dumps(payload)};\n",
                encoding="utf-8",
            )

            entries = load_history(path)

        self.assertEqual(1, len(entries))

    def test_rolling_comparison_uses_last_five_same_run_ratios(self):
        entries = [history_entry(value, 100) for value in (100, 200, 300, 400, 500, 600)]

        comparison = rolling_comparisons(entries, window=5)[0]

        self.assertEqual(5, comparison["runs"])
        self.assertEqual(4.0, comparison["median"])
        self.assertEqual(2.0, comparison["minimum"])
        self.assertEqual(6.0, comparison["maximum"])
        self.assertEqual(1.0, comparison["relative_spread"])

    def test_incomplete_pair_is_not_mixed_with_another_run(self):
        prefix = "Dekaf.Benchmarks.Benchmarks.Client.ConsumerPollBenchmarks"
        parameters = "(MessageSize: 100)"
        entries = [
            {"benches": [benchmark(f"{prefix}.Dekaf_PollSingle{parameters}", 200)]},
            {
                "benches": [
                    benchmark(f"{prefix}.Confluent_PollSingle{parameters}", 100)
                ]
            },
        ]

        self.assertEqual([], rolling_comparisons(entries))

    def test_comparison_missing_from_latest_run_is_not_published(self):
        entries = [
            history_entry(200, 100),
            {"benches": []},
        ]

        self.assertEqual([], rolling_comparisons(entries))

    def test_current_measurement_shape_excludes_older_shape(self):
        prefix = "Dekaf.Benchmarks.Benchmarks.Client.ConsumerPollBenchmarks"
        old_parameters = "(MessageSize: 100)"
        current_parameters = "(PollsPerIteration: 400000, MessageSize: 100)"
        entries = [
            {
                "benches": [
                    benchmark(f"{prefix}.Dekaf_PollSingle{old_parameters}", 200),
                    benchmark(f"{prefix}.Confluent_PollSingle{old_parameters}", 100),
                ]
            },
            {
                "benches": [
                    benchmark(f"{prefix}.Dekaf_PollSingle{current_parameters}", 50),
                    benchmark(f"{prefix}.Confluent_PollSingle{current_parameters}", 100),
                ]
            },
        ]

        comparisons = rolling_comparisons(entries)

        self.assertEqual(1, len(comparisons))
        self.assertEqual(1, comparisons[0]["runs"])
        self.assertEqual(0.5, comparisons[0]["median"])
        self.assertEqual(
            "PollsPerIteration: 400000, MessageSize: 100",
            comparisons[0]["parameters"],
        )

    def test_latest_ratio_sd_is_visibly_flagged(self):
        table = [
            "| Method | Ratio | RatioSD |",
            "|---|---:|---:|",
            "| Dekaf_Fast | 0.50 | 0.10 |",
            "| Dekaf_Noisy | 1.50 | 0.31 |",
        ]

        annotated = annotate_ratio_confidence(table, variance_threshold=0.30)

        self.assertIn(" Confidence ", annotated[0])
        self.assertIn(" Stable ", annotated[2])
        self.assertIn(" ⚠ Low ", annotated[3])

    def test_table_without_ratio_sd_is_not_annotated(self):
        tables = [
            "| Method | Ratio | RatioSD |",
            "|---|---:|---:|",
            "| Dekaf_Noisy | 1.50 | 0.31 |",
            "| Method | Mean | Allocated |",
            "|---|---:|---:|",
            "| Read | 10 ns | - |",
        ]

        annotated = annotate_ratio_confidence(tables, variance_threshold=0.30)

        self.assertIn("⚠ Low", annotated[2])
        self.assertEqual("| Method | Mean | Allocated |", annotated[3])
        self.assertEqual("| Read | 10 ns | - |", annotated[5])

    def test_rolling_table_flags_wide_cross_run_spread(self):
        comparison = rolling_comparisons(
            [history_entry(value, 100) for value in (100, 200, 300)],
            window=5,
        )

        table = format_rolling_table(comparison, variance_threshold=0.30)

        self.assertIn("1.00–3.00", table[2])
        self.assertIn("100%", table[2])
        self.assertIn("⚠ Low", table[2])

    def test_document_contains_rolling_and_latest_confidence_context(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            history_path = root / "data.js"
            history_payload = {
                "entries": {
                    "Dekaf Benchmarks": [
                        history_entry(200, 100),
                        history_entry(220, 100),
                    ]
                }
            }
            history_path.write_text(
                f"window.BENCHMARK_DATA = {json.dumps(history_payload)};",
                encoding="utf-8",
            )
            results = root / "results" / "Client" / "results"
            results.mkdir(parents=True)
            (results / "ConsumerPollBenchmarks-report-github.md").write_text(
                "| Method | Ratio | RatioSD |\n"
                "|---|---:|---:|\n"
                "| Dekaf_PollSingle | 2.20 | 0.31 |\n",
                encoding="utf-8",
            )

            document = generate_document(
                root / "results",
                history_path,
                "2026-07-27 18:00 UTC",
            )

        self.assertIn("## Rolling comparison (last 5 runs)", document)
        self.assertIn("ConsumerPollBenchmarks.PollSingle", document)
        self.assertIn("## Latest run", document)
        self.assertIn("⚠ Low", document)
        self.assertIn("RatioSD", document)


if __name__ == "__main__":
    unittest.main()
