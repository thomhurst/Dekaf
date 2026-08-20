import json
import tempfile
import unittest
from pathlib import Path

from benchmark_docs import (
    annotate_ratio_confidence,
    describe_memory,
    describe_speed,
    describe_speed_range,
    format_rolling_table,
    format_summary_table,
    generate_document,
    latest_alloc_ratios,
    load_history,
    rolling_comparisons,
    summarize_scenarios,
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

    def test_changed_producer_guarantees_start_new_history_series(self):
        prefix = "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks"
        parameters = "(MessageSize: 100, BatchSize: 100)"
        entries = [
            {
                "benches": [
                    benchmark(f"{prefix}.Dekaf_ProduceBatch{parameters}", 40),
                    benchmark(f"{prefix}.Confluent_ProduceBatch{parameters}", 100),
                ]
            },
            {
                "benches": [
                    benchmark(
                        f"{prefix}.Dekaf_ProduceBatchAllIdempotent{parameters}", 50
                    ),
                    benchmark(
                        f"{prefix}.Confluent_ProduceBatchAllIdempotent{parameters}",
                        100,
                    ),
                ]
            },
        ]

        comparisons = rolling_comparisons(entries)

        self.assertEqual(1, len(comparisons))
        self.assertEqual("ProduceBatchAllIdempotent", comparisons[0]["operation"])
        self.assertEqual(1, comparisons[0]["runs"])
        self.assertEqual(0.5, comparisons[0]["median"])

    def test_legacy_producer_labels_describe_each_clients_guarantees(self):
        prefix = "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks"
        parameters = "(MessageSize: 100, BatchSize: 100)"
        entries = [
            {
                "benches": [
                    benchmark(f"{prefix}.Dekaf_ProduceBatch{parameters}", 40),
                    benchmark(f"{prefix}.Confluent_ProduceBatch{parameters}", 100),
                    benchmark(f"{prefix}.Dekaf_FireAndForget{parameters}", 40),
                    benchmark(f"{prefix}.Confluent_FireAndForget{parameters}", 100),
                ]
            }
        ]

        table = format_summary_table(
            summarize_scenarios(rolling_comparisons(entries)), {}
        )

        self.assertIn(
            "Produce — batches (legacy: Dekaf acks=all/idempotent; Confluent acks=leader/non-idempotent)",
            table[2],
        )
        self.assertIn(
            "Produce — fire-and-forget (legacy: Dekaf acks=all/idempotent; Confluent acks=leader/non-idempotent)",
            table[3],
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

    def test_describe_speed_maps_ratio_to_plain_english(self):
        self.assertEqual("2.1× faster", describe_speed(0.47))
        self.assertEqual("11× faster", describe_speed(0.09))
        self.assertEqual("on par", describe_speed(1.05))
        self.assertEqual("on par", describe_speed(0.95))
        self.assertEqual("1.5× slower", describe_speed(1.50))

    def test_describe_speed_range_collapses_matching_verdicts(self):
        self.assertEqual("2.1× faster", describe_speed_range(0.47, 0.47))
        self.assertEqual("2.7×–11× faster", describe_speed_range(0.09, 0.37))
        self.assertEqual("on par to 2.3× faster", describe_speed_range(0.43, 1.02))
        self.assertEqual("1.3×–1.8× slower", describe_speed_range(1.30, 1.80))

    def test_describe_memory_maps_alloc_ratio_to_plain_english(self):
        self.assertEqual("—", describe_memory(None))
        self.assertEqual("zero allocations", describe_memory(0.0))
        self.assertEqual("20× less", describe_memory(0.05))
        self.assertEqual("on par", describe_memory(1.0))
        self.assertEqual("2.0× more", describe_memory(2.0))

    def test_summarize_scenarios_aggregates_parameter_permutations(self):
        prefix = "Dekaf.Benchmarks.Benchmarks.Client.ProducerBenchmarks"
        entries = [
            {
                "benches": [
                    benchmark(
                        f"{prefix}.Dekaf_ProduceBatchAllIdempotent(BatchSize: 100)",
                        40,
                    ),
                    benchmark(
                        f"{prefix}.Confluent_ProduceBatchAllIdempotent(BatchSize: 100)",
                        100,
                    ),
                    benchmark(
                        f"{prefix}.Dekaf_ProduceBatchAllIdempotent(BatchSize: 1000)",
                        110,
                    ),
                    benchmark(
                        f"{prefix}.Confluent_ProduceBatchAllIdempotent(BatchSize: 1000)",
                        100,
                    ),
                ]
            }
        ] * 2

        summaries = summarize_scenarios(rolling_comparisons(entries))

        self.assertEqual(1, len(summaries))
        summary = summaries[0]
        self.assertEqual("ProducerBenchmarks", summary["group"])
        self.assertEqual("ProduceBatchAllIdempotent", summary["operation"])
        self.assertEqual(0.4, summary["best"])
        self.assertEqual(1.1, summary["worst"])
        self.assertEqual(2, summary["stable_rows"])
        self.assertEqual(2, summary["total_rows"])

    def test_single_produce_linger_controls_are_separate_scenarios(self):
        prefix = "Dekaf.Benchmarks.Benchmarks.Client.ProducerSingleBenchmarks"
        entries = [
            {
                "benches": [
                    benchmark(f"{prefix}.Dekaf_ProduceSingleNoLinger(MessageSize: 100)", 80),
                    benchmark(f"{prefix}.Confluent_ProduceSingleNoLinger(MessageSize: 100)", 100),
                    benchmark(f"{prefix}.Dekaf_ProduceSingleLinger5(MessageSize: 100)", 10),
                    benchmark(f"{prefix}.Confluent_ProduceSingleLinger5(MessageSize: 100)", 100),
                ]
            }
        ] * 2

        summaries = summarize_scenarios(rolling_comparisons(entries))

        self.assertEqual(
            ["ProduceSingleNoLinger", "ProduceSingleLinger5"],
            [summary["operation"] for summary in summaries],
        )
        table = format_summary_table(summaries, {})
        self.assertIn("Produce — serial awaited (linger=0)", table[2])
        self.assertIn("Produce — serial awaited (linger=5 ms)", table[3])

    def test_latest_alloc_ratios_reads_dekaf_rows(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "ProducerBenchmarks-report-github.md"
            path.write_text(
                "| Method | Ratio | RatioSD | Alloc Ratio |\n"
                "|---|---:|---:|---:|\n"
                "| **Confluent_ProduceBatch** | **1.00** | **0.02** | **1.00** |\n"
                "| Dekaf_ProduceBatch | 0.43 | 0.01 | 0.05 |\n"
                "| Dekaf_ProduceBatch | 0.53 | 0.01 | 0.03 |\n"
                "| Dekaf_FireAndForget | 1.13 | 0.13 | ? |\n",
                encoding="utf-8",
            )

            ratios = latest_alloc_ratios([path])

        self.assertEqual({"ProduceBatch": 0.04}, ratios)

    def test_summary_table_without_history_reports_placeholder(self):
        table = format_summary_table([], {})

        self.assertIn("No comparable history yet", table[2])

    def test_document_contains_context_and_only_comparison_reports(self):
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
                "| Method | MessageSize | Mean | Allocated | Ratio | RatioSD | Alloc Ratio |\n"
                "|---|---:|---:|---:|---:|---:|---:|\n"
                "| Confluent_PollSingle | 1000 | 100.00 μs | 1000 B | 1.00 | 0.00 | 1.00 |\n"
                "| Dekaf_PollSingle | 1000 | 200.00 μs | 500 B | 2.20 | 0.31 | 0.50 |\n",
                encoding="utf-8",
            )
            for name, method in (
                ("ProducerBenchmarks", "Dekaf_ComparisonBatch"),
                ("ProducerSingleBenchmarks", "Dekaf_ComparisonSingle"),
                ("ProducerModeBenchmarks", "Dekaf_InternalMode"),
                ("AsyncProducerSerdePoolingBenchmarks", "Dekaf_InternalSerde"),
            ):
                (results / f"{name}-report-github.md").write_text(
                    "| Method | Mean |\n"
                    "|---|---:|\n"
                    f"| {method} | 1.00 ns |\n",
                    encoding="utf-8",
                )

            document = generate_document(
                root / "results",
                history_path,
                "2026-07-27 18:00 UTC",
            )

        self.assertIn("## At a glance", document)
        self.assertIn("import ComparisonChart, {ComparisonChartGrid}", document)
        self.assertIn('title="Execution time"', document)
        self.assertIn('title="Managed allocations"', document)
        self.assertIn("200.00 μs (2.0× slower)", document)
        self.assertIn("500 B (2.0× less)", document)
        self.assertIn("Consume — poll a single message", document)
        self.assertIn(
            ":::note Reading producer results\n"
            "The `linger=0` scenario is the matched client comparison. The `linger=5 ms` scenario intentionally measures each client's app-limited batching policy and should not be read as general producer throughput. In the legacy serial-awaited results below, Dekaf sends a sole record immediately while Confluent applies the configured linger; the old benchmark's unused `BatchSize` parameter also duplicated each payload result. In the legacy batch and fire-and-forget results, Dekaf used `acks=all` with idempotence while Confluent used `acks=leader` without idempotence. The page will show the new matched controls after the next run from main.\n"
            ":::",
            document,
        )
        self.assertIn("2.1× slower", document)
        self.assertIn("<details>", document)
        self.assertIn(
            "<summary>Cross-run comparison — last 5 runs, per parameter set</summary>",
            document,
        )
        self.assertIn("ConsumerPollBenchmarks.PollSingle", document)
        self.assertIn("Dekaf_ComparisonBatch", document)
        self.assertIn("Dekaf_ComparisonSingle", document)
        self.assertNotIn("Dekaf_InternalMode", document)
        self.assertNotIn("Dekaf_InternalSerde", document)
        self.assertIn("<summary>Latest run — consumer benchmarks</summary>", document)
        self.assertIn("⚠ Low", document)
        self.assertIn("RatioSD", document)


if __name__ == "__main__":
    unittest.main()
